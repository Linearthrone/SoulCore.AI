---
type: task
prop_id: PROP-20.31
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: FED-01
priority: P1
status: Pending
created: 2026-11-18
title: Step 5 — Identity panel weight / band / ledger
depends_on: PROP-20.30
unblocked_by: docs/agents/reports/PROP-20.30-PM01-accept.md
---

# PROP-20.31 — Identity trait panel (FED)

**Activate:** `@Agents/FED-01.md` — PROP-20.30 Accepted. API fields: `restingTraits`, `contextTraitCatalog`, `contextTraits` (see BED report).

## Do

1. Identity tab shows each resting trait’s **weight**, **band**, and **last ledger reason** from Host (PROP-20.30 API).
2. If context traits are exposed for the current/last turn, show them read-only (earned this turn / empty when none).
3. No fabricated biography; Host unreachable stays unreachable.
4. Build + smoke evidence in report.

## Out of scope

- Editing weights from UI (operator edits stay code/ledger for now)
- Bond counters (step 7)

## Report

`docs/agents/reports/PROP-20.31-FED01-to-PM01.md`
