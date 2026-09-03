---
prop_root: PROP-2-ue-reliable-embodiment
type: task
prop_id: PROP-2.2
legacy_task_id: TASK-211
from: PM-01
to: REX-01
priority: P0
status: Pending
created: 2026-08-19
wave: 31
phase: ue-reliability
title: Victoria travel-cm + loco AnimBP (wrappers, no MHC reparent)
depends_on: PROP-2.1
proposal: docs/archive/proposals/victoria-ue-reliable-embodiment.md
intake: docs/agents/tasks/PROP-2.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-2.2-REX01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
staffing: UE content / AnimBP specialist under REX lane
---

# PROP-2.2: Victoria walks (measured cm) + loco AnimBP

## Problem

ISSUE-006 class failures: API ok, **0 cm** travel; T-pose/slide sold as Pass. Victoria must be the **other person** who visibly walks.

## Solution

1. Character wrappers (not MHC reparent) with CMC + AIController + Recast in **PlayWorld**.
2. Pass = XY travel samples / cm traveled + visible loco AnimBP (Manny→`metahuman_base_skel` MVP ok).
3. Single `:8888`; no duplicate bridge.
4. Optional: separate Kayleigh ABP if shared instance fights.
5. Evidence on **shadow Play** with Kurt (or agent) watching her walk a room / NavMesh path.

## Do not

- MHC reparent.
- Pass on JSON `success` without transform delta.
- Cinema groom/montage library (later).

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Measured cm travel > 0 with evidence |
| 2 | Loco AnimBP drives body (not T-pose slide) |
| 3 | Victoria remains AI — player still Kayleigh |
| 4 | Report cites ISSUE-006 regression checks |

## Reply

`docs/agents/reports/PROP-2.2-REX01-to-PM01.md`
