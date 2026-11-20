---
type: task
prop_id: PROP-18.35
from: PM-01
to: QA-01
priority: P1
status: Pending
created: 2026-11-19
title: Live QA smoke — residue APIs + B3 budget on :7710
depends_on: PROP-18.6c
---

# PROP-18.35 — QA live Host smoke

**Activate:** `@Agents/QA-01.md`

Against **live** `http://127.0.0.1:7710` (not side Host):

1. `/health` — status ok, `idleParkedBudget.advancesCap=3`, `toolMinutesCap=45`
2. `/activities?limit=3` — 200
3. `/journals/topic?subject=optics&limit=3` — 200
4. `/presence/while-you-were-gone?limit=3` — 200
5. Note `:7700` still foreign/stale if probed (do not Fail on it)

Paste real HTTP bodies. Report: `docs/agents/reports/PROP-18.35-QA01-to-PM01.md`
