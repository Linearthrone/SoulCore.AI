---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.6
legacy_task_id: TASK-206
from: PM-01
to: FED-01
priority: P1
status: Pending
created: 2026-08-19
wave: 31
phase: phone-digits
title: Link shrink — status + ComfyUI only (after DIGITS Pass)
depends_on: PROP-1.5
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
intake: docs/agents/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.6-FED01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
---

# PROP-1.6: Victoria Link → status + ComfyUI

## Problem

Once SMS/MMS is the daily phone chat, Link should stop pretending to be the messenger — keep **server status** + **ComfyUI / MediaGen**.

## Solution

1. **Gate:** do not start until PROP-1.5 Pass.
2. Strip or demote chat-as-primary in `House.CompanionAndroid` — status dock + MediaGen remain.
3. Do **not** implement Messenger-class rewrite (`victoria-link-messenger-product.md`) here.
4. Deep-link / copy: “Text Victoria at DIGITS” helper optional.

## Do not

- Gut Link before 205 Pass.
- Build floating chat-heads / theme packs in this ticket.
- PSTN/video duplex.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | After 205, Link primary surfaces = status + Comfy |
| 2 | No second chat brain |
| 3 | Report + screenshot |

## Reply

`docs/agents/reports/PROP-1.6-FED01-to-PM01.md`
