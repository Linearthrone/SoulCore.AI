---
type: routing
prop_id: PROP-15.30
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: PM-01
status: Pass
created: 2026-10-21
updated: 2026-10-21
depends_on: PROP-15.29 (Pass)
proposal: docs/agents/unexecuted_proposals/persona-creation-platform.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: PROP-15 platform closeout — ready for UI rewrite plan
---

# PROP-15.30 — Platform closeout

## Verdict

**Platform v1 complete.** Host gate **15.29 Pass** (`0.2.0` + `capabilities.personas` + lexi/`mother:latest`). LinearThrone next: UI rewrite plan (separate PROP). Presence catch-up from upstream stays **out of band** until after UI plan.

## Delivered (accepted seats)

| Area | Splits |
| --- | --- |
| Pack + quarantine + traits | 15.1–15.2, 15.5, 15.8–15.9 |
| In-app create/edit + switcher | 15.3–15.4, 15.10 |
| Desk E2E | 15.6 / 15.6b |
| Per-persona model | 15.11–15.12, 15.15–15.16b |
| Prompt / merge honesty | 15.17, 15.19–15.20 |
| Currency chrome + naming | 15.21–15.28 |

## Deferred (not UI-rewrite blockers)

| Item | Disposition |
| --- | --- |
| **15.13 / 15.13c / 15.13d** persona stress suite | **Deferred** — Fail / incomplete; confabulation + LIMITS bait under-weighted on current models. Re-open as separate feel-drive PROP after UI rewrite if still needed. |
| Upstream Presence 0.1.8→0.1.48 | Explicitly deferred; LinearThrone will plan UI rewrite first. |
| Commit to `origin/main` | Requires LinearThrone go-ahead (large uncommitted tree). |

## Ops gate

- [x] **15.29** — `:7700` = CreatorEdition Host **0.2.0** + `capabilities.personas=true` + lexi/`mother:latest`

## Proposal status

`persona-creation-platform.md` → `accepted-shipped-platform` (stress residual noted). Registry → **Complete (platform)**; next free work = UI rewrite PROP.
