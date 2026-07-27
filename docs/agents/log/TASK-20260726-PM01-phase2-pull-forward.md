---
type: report
task_id: PM-NOTE-PHASE2
from: PM-01
to: TT-01 / BED-01 / QA-01
status: Completed
completed: 2026-07-26
wave: 27
title: Phase 2 pull-forward — gestures + look ASAP (parallel Phase 1)
---

# PM note: Wave 27 Phase 2 pulled forward

## User decision

**2026-07-26:** "yes move phase 2 asap" — do **not** wait for QA-118. Phase 2 runs **parallel** with Wave 26 Phase 1.

## Tickets (Phase 2)

| ID | Role | Scope | Path |
| --- | --- | --- | --- |
| **BED-119** | BED-01 | `play_animation` JSON `args.name` (2.1) — already ticketed | `tasks/TASK-20260726-119-PM01-to-BED01.md` |
| **BED-120** | BED-01 | `look` / autonomy `args.command` (2.2) — already ticketed | `tasks/TASK-20260726-120-PM01-to-BED01.md` |
| **BED-121** | BED-01 | Gesture + emotion montages `/Game/Animations/Victoria/` (2.3–2.4) | `tasks/TASK-20260726-121-PM01-to-BED01.md` |
| **BED-122** | BED-01 | Head/eye gaze IK for `look` (2.5; **depends on 120**) | `tasks/TASK-20260726-122-PM01-to-BED01.md` |
| **QA-123** | QA-01 | Visual gate: wave visible; look = head not torso | `tasks/TASK-20260726-123-PM01-to-QA01.md` |

## Dispatch order

1. **BED-119 + BED-120** — **STARTED** 2026-07-26 (BED-01 Task agents).
2. **BED-121** — **STARTED** 2026-07-26 (content; parallel capacity).
3. **BED-122** — queued until BED-120 Pass (not started).
4. **QA-123** — queued until 119/120/121/122 as applicable (not started; independent of QA-118).

## PRODUCT_ROOT

Open gate 9 **Scope** cleared. Phase 2 marked in flight; Phases 3–4 remain proposal-only.
