---
type: task
prop_id: PROP-15.21
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: OPS-HOME
priority: P0
status: In Progress
created: 2026-10-05
updated: 2026-10-05
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Recycle :7700 to CreatorEdition Host with /api/personas
---

# PROP-15.21 — Host is pre-persona (blocks switcher)

## Problem

Live `:7700` reports `version=0.1.5` and **`GET /api/personas` → 404**. Presence persona switcher cannot list/activate packs. Health also shows legacy memory path + `gemma4:latest` (old Host tree).

## Action

1. Stop old Host on `:7700`.
2. Start **current CreatorEdition** Release Host from this repo (`dotnet run --project SoulCore/SoulCore.Host -c Release` or ALLSTART).
3. Verify:
   - `GET /health` → `version` from current csproj (bump if still reporting 0.1.5 after rebuild — note for BED)
   - `GET /api/personas` → **200** with packs
   - Prefer activate **lexi** + `mother:latest` if pack exists; else blank
4. Leave ChatDesktop able to refresh switcher (user can open Presence after).

## Report

`docs/agents/reports/PROP-15.21-OPSHOME-to-PM01.md`
