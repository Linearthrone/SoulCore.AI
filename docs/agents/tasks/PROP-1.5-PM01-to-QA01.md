---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.5
legacy_task_id: TASK-205
from: PM-01
to: QA-01
priority: P0
status: Pending
created: 2026-08-19
wave: 31
phase: phone-digits
title: QA — Kurt SMS round-trip + MMS screenshot still
depends_on: PROP-1.1, PROP-1.2, PROP-1.3, PROP-1.4
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
intake: docs/agents/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.5-QA01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
---

# PROP-1.5: DIGITS QA gate

## Sit-down (not log-only)

1. Kurt texts DIGITS from stock Messages → Victoria reply SMS (Host up).
2. Same turn visible in ChatDesktop One Thread.
3. Kurt asks for a screenshot → MMS still arrives; matches her browser/Presence frame.
4. Unknown number silent (if testable with second SIM / spoof harness).
5. Host down: **silence** (PM default) — no ghost Victoria SMS.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | SMS round-trip Pass with evidence (redacted) |
| 2 | MMS still Pass |
| 3 | One Thread confirmed in ChatDesktop |
| 4 | SEC checks from 204 spot-checked |

## Reply

`docs/agents/reports/PROP-1.5-QA01-to-PM01.md`
