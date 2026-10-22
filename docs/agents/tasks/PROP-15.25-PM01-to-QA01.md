---
type: task
prop_id: PROP-15.25
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: QA-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
qa_report: docs/agents/reports/PROP-15.25-QA01-to-PM01.md
depends_on: PROP-15.24 (Pass)
fed_report: docs/agents/reports/PROP-15.24-FED01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Smoke — capabilities.personas currency + naming scrub after 15.24
---

# PROP-15.25 — QA smoke after 15.24

## Prereqs

- Host CreatorEdition **0.2.0+** on `:7700` with `GET /health` → `capabilities.personas: true` and `/api/personas` 200
- Run **current** ChatDesktop Release from this tree (15.24 bits)

## Checks

1. Title bar / Settings → Updates: `Presence … · … · Host 0.2.0 · Personas OK` (not Host outdated).
2. Switcher still lists DisplayNames; one quarantine activate + restore works (spot-check only).
3. Spot naming: Presence system notice / any visible path example must not show machine username tokens; prefer `%USERPROFILE%\Soul_Core` style.
4. Optional (only if easy without killing feel-drive pin): confirm outdated path still makes sense — do **not** break the pinned Host for a long outage.

## Report

`docs/agents/reports/PROP-15.25-QA01-to-PM01.md` — Pass/Fail + chrome string evidence. On Pass note `Ready for SLOP-01`.
