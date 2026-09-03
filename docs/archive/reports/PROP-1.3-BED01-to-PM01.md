---
type: report
prop_id: PROP-1.3
legacy_task_id: TASK-203
from: BED-01
to: PM-01
status: Pass
created: 2026-09-02
role: Backend Engineer
---

# PROP-1.3 BED → PM-01 — Outbound SMS/MMS + screenshot stills

## Result

**Pass (code + unit).** Host enqueues outbound SMS after allowlisted inbound replies, enqueues MMS stills on screenshot-ask (SMS keywords) or `send_screenshot_mms` tool, rate-limits carrier spam, and exposes a poll/ack API for the tablet gateway. Mock queue covered by unit tests (no Android required in CI).

## What shipped

| Piece | Path |
| --- | --- |
| `SmsOptions` outbound knobs | `SoulCore.Config/SmsOptions.cs` |
| Outbound queue + rate limit | `SmsOutboundService` / `ISmsOutboundService` |
| Inbound → auto SMS enqueue | `SmsInboundService` |
| SMS screenshot keywords | `SmsScreenshotAsk` |
| Tool | `send_screenshot_mms` (`SendScreenshotMmsTool`) |
| Poll / ack API | `GET .../sms/outbound/pending`, `POST .../sms/outbound/{id}/ack` |
| Tablet poller | `sms-outbound-poll.sh` |
| Runbook | `docs/runbooks/sms-gateway-inbound.md` (Outbound section) |
| Tests | `SmsOutboundServiceTests` (+ existing Sms*) — **21** passed |

## Acceptance

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Text reply SMS reaches Kurt via gateway | **Code Pass** — Host enqueues; Kurt runs Termux poller or Tasker Send SMS (ops) |
| 2 | Explicit screenshot ask → one MMS still | **Code Pass** — SMS keywords + tool; poller saves still + notifies |
| 3 | No auto-spam; rate limit documented | **Pass** — defaults 12s/30 SMS/h, 60s/6 MMS/h; runbook table |
| 4 | Tests with mock gateway green | **Pass** — 21 Sms* tests |

## Kurt / OPS to go live

1. Pull + restart Host (`ALLSTART` / RestartHost).
2. On tablet: `sms-outbound-poll.sh --loop 10` **or** Tasker Send SMS from `replyText` (not both).
3. Text tablet → expect SMS reply on Kurt’s phone.
4. Text `send me a screenshot` → notification + file under `~/storage/downloads/soulcore-mms/` → send in Messages if OEM blocks Termux MMS.

## Do not

- Auto-MMS every `desktop_screenshot`
- Funnel / public Host bind
- Commit MDNs / tokens
