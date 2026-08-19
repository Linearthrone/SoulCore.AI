---
prop_root: PROP-2-ue-reliable-embodiment
type: task
prop_id: PROP-2.3
legacy_task_id: TASK-212
from: PM-01
to: REX-01
priority: P0
status: Pending
created: 2026-08-19
wave: 31
phase: ue-reliability
title: One Presence still = Victoria eye_frame (Host refuses empty)
depends_on: PROP-2.1
proposal: docs/agents/unexecuted_proposals/victoria-ue-reliable-embodiment.md
intake: docs/agents/tasks/PROP-2.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-2.3-REX01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
bed_assist: BED-01 only if Host still returns success on empty PNG
---

# PROP-2.3: One eye still in Presence

## Problem

If she claims to see, Kurt needs **exactly one** Presence still — Victoria `eye_frame`. Empty capture must be a **tool error**, not a described room. No live her-cam; no second UE feed in that panel.

## Solution

1. SceneCapture on **Victoria Character wrapper** head → `eye_capture` → Host `victoria_eye_capture` / `eye_frame`.
2. Host: empty bytes ⇒ Success:false (BED assist only if currently lying).
3. Presence “What she saw” / eyes slot shows that **one** still — do not park `call_capture` here.
4. Evidence: tool fail on empty; Pass still when capture works on shadow.

## Do not

- Live her-cam PiP in Kurt’s 1P viewport.
- Feed TASK-192 call camera into Presence sight.
- Capture from Kayleigh’s camera.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | One Presence still from Victoria eyes |
| 2 | Empty capture = error |
| 3 | No second UE sight feed in that panel |
| 4 | Screenshot evidence |

## Reply

`docs/agents/reports/PROP-2.3-REX01-to-PM01.md`
