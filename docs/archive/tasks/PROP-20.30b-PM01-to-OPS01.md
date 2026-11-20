---
type: task
prop_id: PROP-20.30b
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-18
title: Release rebuild Host for PROP-20.30 traits
depends_on: PROP-20.30
---

# PROP-20.30b — OPS Release refresh (traits)

**Activate:** `@Agents/OPS-01.md`

`ALLSTART -Configuration Release -RestartHost` (ForceRebuild if locks). Probe `/health` and `GET /settings/identity` for `restingTraits`. Report: `docs/agents/reports/PROP-20.30b-OPS01-to-PM01.md`
