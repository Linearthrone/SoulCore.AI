---
type: report
prop_id: PROP-20.5
from: SEC-01
to: PM-01
status: Completed
result: Pass
completed: 2026-11-17 17:55
---

# PROP-20.5 SEC-01 Completion Report

## Risk Summary

- Severity addressed: **P0** (hostile SMS must not execute house-changing tools)
- Residual risk: Model may still *emit* denied tool-call JSON on the SMS path; Host **refuses execution** via `SmsToolGate` (fail closed). Outbound answer+URL shaping remains BED PROP-20.7. Mind-block prose remains BED PROP-20.4.

## Verdict

**Pass** — SMS inbound may run a **filtered** tool loop; denied/unknown tools never reach the inner registry; MMS bytes are not tool args; health flag matches policy.

## Changes

| File | Change |
| --- | --- |
| `SoulCore.Inference/Presence/SmsToolGate.cs` | Canonical allow/deny gate + `FilterRegistry` / `FilterDefinitions`; `SmsTools` thin alias for BED pipe |
| `SoulCore.Host/Companion/SmsInboundService.cs` | When `IToolRegistry` is wired → `CompleteWithToolsAsync` through `SmsToolGate.FilterRegistry`; MMS caption notes “not tool input” |
| `SoulCore.Host/Companion/SmsHealthSnapshot.cs` | `inboundUsesToolLoop = SmsToolGate.InboundUsesToolLoop` (`true`) |
| `SoulCore.Protocol.Tests/SmsSecurityGateTests.cs` | Rewritten for filtered-loop contract (allow / deny / fail-closed / hostile inbound / MMS) |
| `SoulCore.Protocol.Tests/SmsInboundServiceTests.cs` | Preamble expectations for allow-list vs no-registry paths |

### Allow list (exact)

`browser_navigate`, `browser_snapshot`, `browser_health`, `browser_tabs`, `browser_back`, `browser_scroll`, `browser_click_text`, `desktop_screenshot`, `memory_recall`, `memory_search`, `research_subject_list`, `research_subject_open`, `research_paper_list`, `research_paper_get`, `research_paper_write`

### Deny / fail-closed

Explicit deny (desktop control, browser fill/type/key, `guest_shell` / `guest_write*`, email write, MT4 write, `workflow_execute`, task release/delete/update_status). **Unknown tools → refused.** Any `guest_*` refused.

## Acceptance checklist

| Criterion | Result |
| --- | --- |
| Denied tool never executes from SMS inbound | **Pass** — `Inbound_HostileSms_DeniedToolsNeverExecute` |
| Allowlisted tool path permitted by gate | **Pass** — `Inbound_AllowlistedTool_MayExecuteThroughGate` + allow-list theories |
| MMS bytes not passed as tool args | **Pass** — `Inbound_MmsBytes_NeverPassedAsToolArgs` |
| Health `inboundUsesToolLoop` truthful | **Pass** — asserts `true` |
| Report filed | **Pass** — this file |

## Defensive Verification (actual output)

Command:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~SmsSecurityGateTests|FullyQualifiedName~SmsInboundServiceTests|FullyQualifiedName~SmsE164Tests"
```

Result:

```text
Passed!  - Failed:     0, Passed:    72, Skipped:     0, Total:    72, Duration: 376 ms - SoulCore.Protocol.Tests.dll (net8.0)
```

Key defensive cases covered in `SmsSecurityGateTests`:

- `SmsToolGate_DenyList_Refused` / `SmsToolGate_Unknown_FailClosed` — unit fail-closed
- `SmsToolGate_FilterRegistry_DeniedNeverReachesInner` — registry refuse, inner `Executed` empty
- `Inbound_HostileSms_DeniedToolsNeverExecute` — hostile prompt path uses tool loop; `desktop_open_app` / `guest_shell` / `email_send` / `mt4_order` attempted; **inner registry never executed**
- `Inbound_AllowlistedTool_MayExecuteThroughGate` — `memory_recall` advertised + executed; `desktop_open_app` not advertised
- `Inbound_MmsBytes_NeverPassedAsToolArgs` — no `Images` on messages; media note “not tool input”; PNG bytes not in tool args
- `UnknownSender_SilentDrop_NoToolLoop` — phone allowlist still enforced
- Redaction / EXIF / loopback bind — retained from PROP-1.4

## Follow-ups for PM

- **QA-01**: carrier/live SMS round-trip with allowlisted `browser_navigate` / `memory_recall` and a hostile “open Terminal” SMS (expect refuse, no desktop open).
- **BED-01 (20.7)**: owns answer + public URL product shaping (already consuming `SmsToolGate` / alias).
- Do **not** widen LeisureTools with SMS allow list (PROP-20.6).
