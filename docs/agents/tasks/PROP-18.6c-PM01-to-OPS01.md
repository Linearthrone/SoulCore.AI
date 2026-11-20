---
type: task
prop_id: PROP-18.6c
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-19
title: Release Host rebuild — land residue APIs
depends_on: PROP-18.6b
---

# PROP-18.6c — OPS Host rebuild (residue routes)

**Activate:** `@Agents/OPS-01.md`

Live Host after 18.6b is healthy and has migration 015, but `GET /activities`, `/journals/topic`, `/presence/while-you-were-gone` return **404** (Release DLL predates current `WebApplicationExtensions` MapGets).

1. `ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild` (SkipVoice/SkipPlaywright ok).
2. Probe and paste: `/health`, `/activities?limit=3`, `/journals/topic?subject=optics&limit=3`, `/presence/while-you-were-gone?limit=3` — expect non-404 (empty 200 ok).
3. Update `deploy_history.json` + `docs/版本变更记录.md` if this is the residue-API landing build.

Report: `docs/agents/reports/PROP-18.6c-OPS01-to-PM01.md`
