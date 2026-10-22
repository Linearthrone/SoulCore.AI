---
type: task
prop_id: PROP-15.28
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: SLOP-01
priority: P2
status: Done
created: 2026-10-05
updated: 2026-10-05
slop_report: docs/agents/reports/PROP-15.28-SLOP01-to-PM01.md
depends_on: PROP-15.27b (Pass)
qa_report: docs/agents/reports/PROP-15.27b-QA01-to-PM01.md
fed_report: docs/agents/reports/PROP-15.27-FED01-to-PM01.md
prior_slop: docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Post-QA slop — FormatPathForDisplay RepoRoot redact
---

# PROP-15.28 — Slop audit after 15.27b Pass

## Scope

- `LocalStackControl.FormatPathForDisplay` + call sites (Presence notice, Settings `Repo:` progress)
- Related tests in `LocalStackControlTests`
- Confirm 15.26 F1 closed; no leftover raw-RepoRoot user-visible interpolations in the same surfaces

## Do not

- Modify product code; report only
- Expand into Host / currency / stress suite

## Report

`docs/agents/reports/PROP-15.28-SLOP01-to-PM01.md`
