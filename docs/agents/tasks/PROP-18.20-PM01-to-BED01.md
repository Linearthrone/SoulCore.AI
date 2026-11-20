---
type: task
prop_id: PROP-18.20
from: PM-01
to: BED-01
priority: P1
status: Pending
created: 2026-11-19
title: SLOP cleanup — daybook provenance / parse / truncate
depends_on: PROP-18.17
---

# PROP-18.20 — BED hygiene (PROP-18.17 F1–F3)

**Activate:** `@Agents/BED-01.md`

1. **F1** — One shared journal/activity provenance formatter for `DaybookTape` + `RecallTimelineTool`; activity empty-field text must match.
2. **F2** — Reuse `MemoryTape`/`DaybookTape` occurred-at parse in the tool (no twin parser).
3. **F3** — One truncate helper with limit params (desk vs tool budgets).

Tests green. Report: `docs/agents/reports/PROP-18.20-BED01-to-PM01.md`
