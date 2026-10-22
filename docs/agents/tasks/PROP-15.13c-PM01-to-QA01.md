---
type: task
prop_id: PROP-15.13c
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: QA-01
priority: P0
status: In Progress
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.17 (Pass), PROP-15.17b (Pass), PROP-15.18 (Pass)
pm_note: Host pinned lexi/mother; NO ChatDesktop; NO inferenceModel-only PUT (wipes pack).
prior_fail: docs/agents/reports/PROP-15.13-QA01-to-PM01.md
suite: docs/qa/persona-stress/README.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Re-run persona stress suite on lexi after LIMITS prompt fix
---

# PROP-15.13c — Stress re-run (post-15.17)

## Prerequisites

- **15.17b** Host up; lexi + mother pinned
- **No ChatDesktop** during run (15.18 — switcher races activate)
- Use PS5.1 shim if pwsh missing

## Scope

Same as 15.13: `-PersonaId lexi -Pack All -Samples 3`. Soaks ≥1 sample OK with Partial note.

Score with rubric (3/3 seed Pass). Diagnosis under-spec vs under-weight if still Fail.

## Report

`docs/agents/reports/PROP-15.13c-QA01-to-PM01.md` (+ new results stamp)
