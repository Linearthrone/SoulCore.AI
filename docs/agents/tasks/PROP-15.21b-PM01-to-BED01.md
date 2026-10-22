---
type: task
prop_id: PROP-15.21b
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: BED-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.21 (Pass)
ops_report: docs/agents/reports/PROP-15.21-OPSHOME-to-PM01.md
pm_note: OPS confirmed 404 was wrong-tree Host. Still bump Version + capabilities.personas so Presence can tell CreatorEdition Host from LinearThrone 0.1.5.
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Bump Host Version so /health cannot lie as 0.1.5 after persona platform
---

# PROP-15.21b — Host version honesty

After OPS recycles current tree, if `/health.version` still reads `0.1.5` while PROP-15 APIs exist, bump `SoulCore.Host.csproj` `<Version>` / `<InformationalVersion>` (e.g. 0.2.0) so Presence chrome can tell “new Host” from pre-persona builds. Expose optional `capabilities.personas=true` on `/health` if cheap.

Report: `docs/agents/reports/PROP-15.21b-BED01-to-PM01.md`
