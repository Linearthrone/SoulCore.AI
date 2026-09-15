---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.4
legacy_task_id: TASK-204
from: PM-01
to: SEC-01
priority: P0
status: Completed — Partial (code + tests on main; live tablet round-trip = PROP-1.5)
tina_wave_now: 2026-09-05 — reaffirmed by TINA program accept
created: 2026-08-19
updated: 2026-09-15
wave: 31
phase: phone-digits
title: DIGITS channel security — allowlist, no Funnel, no inbound tools
depends_on: PROP-1.2
proposal: docs/archive/proposals/victoria-digits-sms-channel.md
intake: docs/archive/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.4-SEC01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
---

# PROP-1.4: DIGITS SEC gates

## Solution

1. operator-only E.164 allowlist required before any outbound/inbound processing.
2. Inbound SMS/MMS **never** enters tool-loop / ForceTool / desktop control.
3. No Funnel; no non-loopback Host bind for this feature.
4. DIGITS number + tokens never in git, `/health`, or logs (length/bool only).
5. Gateway auth token rotation notes; strip EXIF on outbound MMS if needed.
6. Threat note: bot must not sit on operator’s **primary** SMS graph.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Written allowlist + drop behavior verified |
| 2 | Attempted inbound tool-injection refused |
| 3 | Checklist: Funnel off, MDN not in repo |
| 4 | Report cites kill criteria from PROP §8 |

## Reply

`docs/agents/reports/PROP-1.4-SEC01-to-PM01.md`

## Status (2026-09-15 registry reconcile)

Ticket was still `Pending` after the SEC slice landed on `main` and its report was rescued via
PR #87. Corrected to `Completed — Partial`.

On `main`: `SoulCore/SoulCore.Host/Companion/SmsHealthSnapshot.cs`,
`SmsMmsImageSanitizer.cs`, and `SoulCore/SoulCore.Protocol.Tests/SmsSecurityGateTests.cs`.

Residual is **not** carried here — live operator tablet SMS/MMS round-trip is `PROP-1.5`
(QA-01, still `Pending`). Left in `docs/agents/tasks/` rather than archived because the PROP-1
root still has open lanes (1.5, 1.6).
