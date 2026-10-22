---
type: task
prop_id: PROP-15.17b
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: OPS-HOME
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.17 (Pass), PROP-15.18 (Pass)
bed_report: docs/agents/reports/PROP-15.17-BED01-to-PM01.md
ops_pin: docs/agents/reports/PROP-15.18-OPSHOME-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Restart Host after 15.17; pin lexi for stress re-run
---

# PROP-15.17b — Host recycle + lexi pin

1. Quit/freeze ChatDesktop (no Presence switcher on `:7700`).
2. Start current Release Host on `127.0.0.1:7700`.
3. Ensure lexi `inferenceModel=mother:latest`; activate lexi.
4. Gate `/health`: active lexi, `resolvedInferenceModel=mother:latest`, `modelSource=persona`.

Report: `docs/agents/reports/PROP-15.17b-OPSHOME-to-PM01.md`
