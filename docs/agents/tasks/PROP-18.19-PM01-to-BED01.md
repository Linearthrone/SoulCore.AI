---
type: task
prop_id: PROP-18.19
from: PM-01
to: BED-01
priority: P2
status: Pending
created: 2026-11-19
title: SLOP cleanup — health budget snapshot + IsExemptKind
depends_on: PROP-18.14
---

# PROP-18.19 — BED hygiene (PROP-18.14 F1/F2)

**Activate:** `@Agents/BED-01.md`

1. **F1** — `/health` use `SafeParkedBudgetSnapshot` (remove duplicate try/catch).
2. **F2** — Keep `IsExemptKind`; ensure production exempt paths call it (no hard-coded kind lists elsewhere).

Tests green. Report: `docs/agents/reports/PROP-18.19-BED01-to-PM01.md`
