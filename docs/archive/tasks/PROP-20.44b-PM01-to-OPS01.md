---
type: task
prop_id: PROP-20.44b
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-18
title: Release ALLSTART — ship 1.0.0
depends_on: PROP-20.44
unblocked_by: docs/agents/reports/PROP-20.44-PM01-accept.md
---

# PROP-20.44b — OPS ship 1.0.0

**Activate:** `@Agents/OPS-01.md` — PROP-20.44 Accepted.

`ALLSTART -Configuration Release -RestartHost -ForceRebuild`. Probe `/health` → **version 1.0.0**. Presence Release up. Update `deploy_history.json` / version changelog if present in tree; otherwise note absence.

Report: `docs/agents/reports/PROP-20.44b-OPS01-to-PM01.md`
