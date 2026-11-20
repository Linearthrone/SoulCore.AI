---
type: task
prop_id: PROP-18.6b
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-19
title: Host restart — apply migration 015 activities log
depends_on: PROP-18.6
---

# PROP-18.6b — OPS apply 015

**Activate:** `@Agents/OPS-01.md`

Release ALLSTART RestartHost so migration **015** (`victoria_activities_log`) applies. Probe health; confirm schema_migrations has 015. Report: `docs/agents/reports/PROP-18.6b-OPS01-to-PM01.md`
