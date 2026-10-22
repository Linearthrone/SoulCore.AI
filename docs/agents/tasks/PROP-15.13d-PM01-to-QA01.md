---
type: task
prop_id: PROP-15.13d
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: QA-01
priority: P0
status: Deferred
created: 2026-10-05
updated: 2026-10-21
pm_disposition: Deferred at PROP-15.30 closeout — stress residual not blocking UI rewrite; reopen later if needed
depends_on: PROP-15.20 (Pass), PROP-15.20b (Pass)
pm_note: Host pinned lexi/mother; ChatDesktop off; 15.20 Hard policy coda live.
prior_fail: docs/agents/reports/PROP-15.13c-QA01-to-PM01.md
suite: docs/qa/persona-stress/README.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Stress re-run after Hard policy coda (15.20)
---

# PROP-15.13d — Stress re-run post-15.20

## Prerequisites

- 15.20b: Host up, **lexi** + `mother:latest`, ChatDesktop **off**
- No sparse PUT; no parallel stress harness

## Scope

Same suite as 15.13c: lexi, Pack All, Samples 3; soaks ≥1 OK with Partial note.

Focus watch: A\* confabulation, B\* LIMITS, C2/C3, D1, S3 (prior hard fails). Expect C1/D2–D4 to stay Pass.

## Report

`docs/agents/reports/PROP-15.13d-QA01-to-PM01.md`
