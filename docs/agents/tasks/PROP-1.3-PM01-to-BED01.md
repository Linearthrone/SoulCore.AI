---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.3
legacy_task_id: TASK-203
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-08-19
wave: 31
phase: phone-digits
title: Outbound SMS/MMS + send_screenshot_mms (Playwright/Presence still)
depends_on: PROP-1.2
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
intake: docs/agents/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.3-BED01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
---

# PROP-1.3: Outbound SMS/MMS + screenshot stills

## Problem

Phone observer is **MMS stills on ask**, not a live Link stream. Victoria must be able to push a Playwright/Presence frame to Kurt’s Messages.

## Solution

1. Host → gateway outbound SMS for `chat.done` / short replies (rate-limit; no SoulLoop spam onto carrier).
2. Tool or companion verb: `send_screenshot_mms` (name flexible) — grabs current Victoria browser view hub JPEG **or** last Presence capture → gateway MMS.
3. **Opt-in / on ask** only — not every tool click.
4. Apply same gallery/redact rules (no secret EXIF dump; prefer in-memory Playwright frame).
5. Wire stub/mock gateway for CI when no Android present.

## Do not

- Live phone stream of her browser.
- Auto-MMS every desktop_screenshot.
- Voice/PSTN.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Text reply SMS reaches Kurt via gateway |
| 2 | Explicit screenshot ask → one MMS still |
| 3 | No auto-spam; rate limit documented |
| 4 | Tests with mock gateway green |

## Reply

`docs/agents/reports/PROP-1.3-BED01-to-PM01.md`
