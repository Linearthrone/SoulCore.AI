---
type: task
prop_id: PROP-20.18
from: PM-01
to: BED-01
priority: P1
status: Done
created: 2026-11-17
title: Remove CharterFloorMap + dedupe leisure Constitution titles
depends_on: PROP-20.17
slop: docs/agents/reports/PROP-20.17-SLOP01-to-PM01.md
---

# PROP-20.18 — BED step-4 slop cleanup

**Activate:** `@Agents/BED-01.md`

1. **F1** Delete `CharterFloorMap.cs` (and any project refs). Callers stay on `CharterBooks`.
2. **F2** Leisure provisional floor titles from `CharterBooks.ConstitutionTitles` (bodies may stay local snapshot).
3. **F3** Do not extract shared desk/SMS loader now.

Tests for Charter/VictoriaMindBlock/Leisure — paste output. Report: `docs/agents/reports/PROP-20.18-BED01-to-PM01.md`
