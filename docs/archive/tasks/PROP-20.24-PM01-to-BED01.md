---
type: task
prop_id: PROP-20.24
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-17
title: Operator resting weights Dominant 0.70 / Ambitious 0.80 / Uninhibited 0.80
decision: docs/agents/unexecuted_proposals/prop-20-resting-seed-decision.md
blocks: PROP-20.1d
---

# PROP-20.24 — Operator resting weights

**Activate:** `@Agents/BED-01.md`

## Set in `DefaultRestingTraits`

| Name | Weight | Band | Ledger |
| --- | ---: | --- | --- |
| Dominant | 0.70 | Mid (below High edge 0.80) | `operator` |
| Ambitious | 0.80 | High | `operator` |
| Uninhibited | 0.80 | High | `operator` |

Leave Possessive, Affectionate, Driven, Devoted, Intense, Proud at **0.52 Mid** with `operator-provisional-until-seed`.

Update tests that assert all traits are 0.52 / same ledger. Report: `docs/agents/reports/PROP-20.24-BED01-to-PM01.md`
