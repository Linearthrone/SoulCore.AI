---
type: task
prop_id: PROP-20.37b
from: PM-01
to: OPS-01
priority: P0
status: Done
created: 2026-11-18
title: Release Host + migration 014 (growth)
depends_on: PROP-20.37
---

# PROP-20.37b — OPS Release + growth migration

**Activate:** `@Agents/OPS-01.md`

`ALLSTART -Configuration Release -RestartHost` (ForceRebuild if locks). Confirm migration **014** applied and `GET /settings/identity` has `bondCounters` / `soak`. Report: `docs/agents/reports/PROP-20.37b-OPS01-to-PM01.md`
