---
type: pm-gate-hold
prop_range: PROP-7..PROP-11
from: PM-01
to: BED-01
status: Accepted-gated
created: 2026-09-05
title: Wipeout Wave-After holds — do not start until gates Pass
---

# PROP-7..11 — Accepted, gated (no execution yet)

| PROP | Role when released | Gate |
| --- | --- | --- |
| PROP-7 | BED | PROP-5 Pass |
| PROP-11 | BED (+ QA soak) | PROP-5 Pass |
| PROP-9 | BED | PROP-5 + PROP-7 Pass |
| PROP-10 | BED | PROP-7 Pass |
| PROP-8 | BED + QA | PROP-5 Pass; **prefer PROP-9 Pass first** |

Intakes remain: `PROP-{N}-TT01-to-PM01.md`.  
Proposals remain in `unexecuted_proposals/`.  
PM will mint `PROP-N.M` execution tickets only when the gate clears — do not pre-start Host work that collides with PROP-5.
