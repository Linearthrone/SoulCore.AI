---
type: task
prop_id: PROP-15.19b
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: OPS-HOME
priority: P0
status: Done
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.19 (Pass)
bed_report: docs/agents/reports/PROP-15.19-BED01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Recycle Host after 15.19; re-pin lexi for stress QA
---

# PROP-15.19b

BED stopped Release Host for rebuild. Restart on `:7700`, keep ChatDesktop off, re-pin **lexi** + `mother:latest` (full GETâ†’merge PUT if needed â€” sparse PUT is now safe but prefer activate after GET verify blurb present). Gate `/health`.

Report: `docs/agents/reports/PROP-15.19b-OPSHOME-to-PM01.md`
