---
type: task
prop_id: PROP-20.34
from: PM-01
to: BED-01
priority: P2
status: Pending
created: 2026-11-18
title: SLOP cleanup — emotion double-read + applies-when scorer merge
depends_on: PROP-20.33
---

# PROP-20.34 — BED hygiene (PROP-20.33 F1/F2)

**Activate:** `@Agents/BED-01.md`

From `docs/agents/reports/PROP-20.33-SLOP01-to-PM01.md`:

1. **F1** — `ChatContextBuilder`: stop second `_emotion.GetAsync` for flat-affect label; reuse emotion snapshot / label from the preamble path.
2. **F2** — `VictoriaMindBlock`: merge `ScoreAppliesWhen` / `ScoreContextAppliesWhen` into one private helper (name/title + tag list).

Keep selection semantics + tests green. Report: `docs/agents/reports/PROP-20.34-BED01-to-PM01.md`
