---
type: task
prop_id: PROP-20.32
from: PM-01
to: QA-01
priority: P0
status: Pending
created: 2026-11-18
title: QA Step 5 context-trait Host selection (PROP-20.30)
depends_on: PROP-20.30
---

# PROP-20.32 — QA trait framework

**Activate:** `@Agents/QA-01.md`

Verify PROP-20.30: resting always on; no match → no `[Context traits]`; match → ≤3; ≥24h stale → zero context; flat affect excludes heat/shadow; Identity JSON has `restingTraits` + `contextTraitCatalog`. Paste real `dotnet test` / probe output.

Report: `docs/agents/reports/PROP-20.32-QA01-to-PM01.md`
