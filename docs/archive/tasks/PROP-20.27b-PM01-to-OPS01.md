---
type: task
prop_id: PROP-20.27b
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-18
title: Release rebuild Host+Presence for Compass B
depends_on: PROP-20.27
---

# PROP-20.27b — OPS Release refresh

**Activate:** `@Agents/OPS-01.md`

`ALLSTART -Configuration Release -RestartHost` (ForceRebuild if locks). Probe `/health`. Report: `docs/agents/reports/PROP-20.27b-OPS01-to-PM01.md`
