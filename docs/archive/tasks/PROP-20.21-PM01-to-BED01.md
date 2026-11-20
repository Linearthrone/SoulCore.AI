---
type: task
prop_id: PROP-20.21
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-17
title: Resting set +Ambitious +Proud (nine Mid 0.52)
decision: docs/agents/unexecuted_proposals/prop-20-resting-seed-decision.md
---

# PROP-20.21 — Expand resting traits

**Activate:** `@Agents/BED-01.md`

## Change

Extend `VictoriaMindBlock.DefaultRestingTraits` (and any mirrored lists/tests) from 7 → **9**:

Keep: Dominant, Possessive, Affectionate, Driven, Devoted, Intense, Uninhibited  
Add: **Ambitious**, **Proud**

Weight **0.52** Mid; ledger `operator-provisional-until-seed`. Short prompt lines for the two new traits (tone only — do not invent floors).

Update tests that hardcode count 7 / name lists (VictoriaMindBlockTests, ChatContextBuilder, LeisureMindBlock / Prop17Leisure as needed).

## Do not

- Full trait table / Host context-trait selection (rest of step 5) — later ticket when weights arrive
- Shadow traits
- Compass-in-prompt

## Acceptance

- [ ] Fresh mind block contains all nine resting names at 0.52 Mid
- [ ] Tests updated and green (paste output)
- [ ] Report: `docs/agents/reports/PROP-20.21-BED01-to-PM01.md`
