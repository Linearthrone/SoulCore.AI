---
type: report
prop_id: PROP-1.4
from: SEC-01
to: PM-01
status: Completed
created: 2026-09-05
branch: cursor/prop14-sms-sec-8a1f
base: cursor/prop5-sqlite-gate-8a1f
verdict: Partial
---

# PROP-1.4 — SEC-01 DIGITS/SMS security gates

## Verdict: **Partial**

Code paths, unit tests, and Host `/health` smoke **Pass** for every SEC gate verifiable in this cloud VM. **Device/carrier SMS round-trip is not provable here** (no Samsung SM-X218U gateway, no Kurt handset on carrier). QA gate **PROP-1.5** remains blocked on live tablet evidence.

## Acceptance criteria

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Written allowlist + drop behavior verified | **Pass** | `SmsE164.ParseAllowlist` / `IsAllowlist`; empty allowlist fail-closed; `SmsInboundService` silent drop (`dropped:true`); `SmsSecurityGateTests.UnknownSender_*`, `SmsInboundServiceTests.UnknownSender_*` |
| 2 | Inbound tool-injection refused | **Pass** | SMS path uses `CompleteAsync` only (never `CompleteWithToolsAsync`); preamble forbids tools; MMS stored as media not tool args; `SmsSecurityGateTests.Inbound_ToolInjectionPrompt_*`, `Inbound_MmsBytes_*` |
| 3 | Checklist: Funnel off, MDN not in repo | **Pass** | `HostBindOptions` default `127.0.0.1`; `Program.cs` SEC-004 throws on non-loopback; `tailscale-serve-soulcore.ps1` tailnet-only (no Funnel); repo scan: no committed Kurt/Victoria MDNs (placeholders `+1XXXXXXXXXX` / test `+15551234567` in tests only) |
| 4 | Secrets not in git / `/health` / logs | **Pass** | `SmsE164.Redact` in log sites; new `SmsHealthSnapshot` exposes bool/length only; live curl below; `.env.example` commented placeholders only |
| 5 | Outbound MMS EXIF strip | **Pass** | `SmsMmsImageSanitizer` nulls EXIF/ICC/XMP before enqueue; `SmsSecurityGateTests.SmsMmsImageSanitizer_*`, `OutboundMms_JpegExifStrippedInPendingJob` |
| 6 | Gateway auth rotation notes | **Pass** | `docs/runbooks/sms-gateway-inbound.md` Security section updated |
| 7 | Live Kurt SMS / tablet gateway | **Not tested** | Cloud VM has no Tasker/Termux/carrier path — **honest Partial** |

## Implementation (this branch)

| File | Change |
| --- | --- |
| `SoulCore.Host/Companion/SmsHealthSnapshot.cs` | `/health` SMS block: allowlistConfigured, counts, mdn length, `inboundUsesToolLoop:false` |
| `SoulCore.Host/Companion/SmsMmsImageSanitizer.cs` | Outbound MMS JPEG metadata strip |
| `SoulCore.Host/Companion/SmsOutboundService.cs` | Sanitize before MMS enqueue |
| `SoulCore.Host/Program.cs` | Wire `sms = SmsHealthSnapshot.Build(...)` on `/health` |
| `SoulCore.Protocol.Tests/SmsSecurityGateTests.cs` | 9 SEC-focused tests |
| `docs/runbooks/sms-gateway-inbound.md` | Token rotation + dedicated-line threat note |

Pre-existing (PROP-1.2/1.3, verified unchanged): Kurt allowlist on inbound/outbound, silent stranger drop, companion API auth, rate limits, screenshot ask keywords without tool-loop on SMS path.

## Commands run

```bash
git checkout cursor/prop14-sms-sec-8a1f

cd SoulCore
dotnet test SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
  --filter "FullyQualifiedName~Sms" --verbosity minimal
# → Passed: 30, Failed: 0

# Host /health smoke (inference/unreal off; test allowlist env)
SOULCORE_Inference__Enabled=false SOULCORE_UnrealBridge__ConnectOnStartup=false \
SOULCORE_Sms__KurtAllowlistE164="+15551234567" \
SOULCORE_Sms__VictoriaMdn="+15559876543" \
  dotnet run --project SoulCore.Host/SoulCore.Host.csproj --no-build &
curl -s http://127.0.0.1:7700/health
```

## `/health` sms block (live smoke, redacted)

```json
{
  "allowlistConfigured": true,
  "allowlistCount": 1,
  "victoriaMdnConfigured": true,
  "victoriaMdnLength": 12,
  "outboundEnabled": true,
  "autoReplyEnabled": true,
  "inboundUsesToolLoop": false
}
```

No raw E.164 or tokens in JSON body (verified by assertion script).

## PROP §8 kill criteria (proposal)

| Kill criterion | Status in tree |
| --- | --- |
| Funnel / bind Host public | **Clear** — loopback-only bind enforced; Tailscale serve docs forbid Funnel |
| Auto-reply to strangers | **Clear** — allowlist gate; outbound enqueue rejects non-allowlisted `toE164` |
| Number in git / health | **Clear** — placeholders only in git; health bool/length |
| Inbound MMS → tools | **Clear** — media attachment + `CompleteAsync` only |
| Bot on **primary** SMS graph | **Ops assumption** — runbook documents dedicated tablet MDN (SM-X218U); not machine-verifiable here |
| Gut Link before SMS/MMS Pass | **Clear** — Link unchanged; PROP-1.6 still gated on PROP-1.5 |
| 911 / emergency origin | **Not implemented** (no emergency SMS path in Host) |

## Threat notes (for PM / QA)

1. **Dedicated line:** Victoria gateway must stay on tablet MDN, not Kurt's daily SIM (PROP §8).
2. **Companion token:** Rotate `SOULCORE_COMPANION_API_TOKEN` with tablet `SOULCORE_TOKEN` together; never commit or log.
3. **Screenshot MMS:** Opt-in on ask only; EXIF stripped on outbound JPEG; stills may still show screen secrets (Presence gallery rules apply).
4. **Inbound prompt injection:** Model may *say* tool-like text; Host does not execute tools on SMS path — WS/desktop tool-loop remains separate.

## What QA-01 must still prove (PROP-1.5)

- Kurt → tablet MDN → Host → reply SMS on carrier
- MMS still round-trip
- Host-down silence policy
- Cannot be executed in this cloud agent environment

## Recommendation

- Mark **PROP-1.4 Partial Pass** — SEC code gates satisfied; unblock **PROP-1.5 QA** on tablet.
- Do **not** mark full Pass until QA-01 cites redacted carrier evidence.
