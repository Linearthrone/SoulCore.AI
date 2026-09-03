---
type: report
prop_id: PROP-1.2
legacy_task_id: TASK-202
from: BED-01
to: PM-01
status: Pass
created: 2026-08-21
role: Backend Engineer
---

# PROP-1.2 BED → PM-01 — Inbound SMS/MMS → One Thread

## Result

**Pass (code).** Host accepts gateway POSTs on `POST /api/companion/v1/messages/inbound`, allowlists Kurt’s E.164, stores MMS as companion media (not tool input), runs a **no-tools** chat turn on `presence-local`, and fans out user + assistant `chat.done` frames on Presence WS so ChatDesktop can show the same thread.

## What shipped

| Piece | Path |
| --- | --- |
| `SmsOptions` | `SoulCore.Config/SmsOptions.cs` |
| Allowlist / E.164 | `SoulCore.Host/Companion/SmsE164.cs` |
| Inbound service | `SoulCore.Host/Companion/SmsInboundService.cs` |
| HTTP route | `CompanionApiEndpoints` → `/messages/inbound` |
| MMS store | `ICompanionMediaService.StoreInboundAsync` |
| ChatDesktop user bubble | `MainWindow.Presence` handles `role=user` |
| Runbook | `docs/runbooks/sms-gateway-inbound.md` |
| Env knobs | `SoulCore/.env.example` |

## Acceptance

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Allowlisted SMS → same Host thread (`presence-local`) | **Pass** (history + WS broadcast + episodic) |
| 2 | Unknown sender dropped | **Pass** (unit) |
| 3 | Inbound image = media, not tool input | **Pass** (unit; CompleteWithTools never called) |
| 4 | Release build + tests | **Pass** — 11 Sms* tests; Host + ChatDesktop Release 0 errors |

## Kurt / OPS to go live

1. Set `SOULCORE_COMPANION_API_TOKEN` and `SOULCORE_Sms__KurtAllowlistE164` (daily phone) in `SoulCore/.env` — **never commit**.
2. Restart Host; ensure Tailscale serve to loopback `:7700`.
3. From tablet Termux, smoke-curl per runbook; then wire SMS→POST (Tasker / SMS gateway app).
4. **PROP-1.3** will SMS `replyText` back to Kurt automatically.

## Do not

- Funnel / non-loopback Host bind
- Commit MDNs
- Expect tools from inbound SMS (explicitly disabled)
