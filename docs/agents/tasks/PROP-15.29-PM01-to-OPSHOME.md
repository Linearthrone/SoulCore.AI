---
type: task
prop_id: PROP-15.29
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: OPS-HOME
priority: P0
status: Pass
created: 2026-10-21
updated: 2026-10-21
report: docs/agents/reports/PROP-15.29-OPSHOME-to-PM01.md
depends_on: PROP-15.21c (Pass)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Recycle :7700 to CreatorEdition Host 0.2.0 — wrong-tree 0.1.5 again
---

# PROP-15.29 — Host recycle for PROP-15 closeout

## Problem

Live `:7700` reports **`version=0.1.5`**, health **missing `capabilities.personas`**, inference shows Host `gemma4:latest` (model missing). Persona switcher / currency chrome will show Host outdated. ALLSTART just brought stack up but picked the wrong Host binary again.

## Do

1. Stop whatever owns `:7700`.
2. Build + start **this repo** `SoulCore/SoulCore.Host` **Release** (`Version` 0.2.0 in csproj).
3. Confirm `GET /health` → `version=0.2.0`, `capabilities.personas=true`, `GET /api/personas` 200.
4. Activate **lexi**; ensure resolved model is an **installed** Ollama tag (`mother:latest` preferred — do not leave gemma4 if missing).
5. Leave ChatDesktop alone if user has it open; note PIDs.

## Report

`docs/agents/reports/PROP-15.29-OPSHOME-to-PM01.md`
