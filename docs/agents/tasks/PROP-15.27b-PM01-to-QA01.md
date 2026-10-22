---
type: task
prop_id: PROP-15.27b
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: QA-01
priority: P2
status: Pass
created: 2026-10-05
updated: 2026-10-05
qa_report: docs/agents/reports/PROP-15.27b-QA01-to-PM01.md
depends_on: PROP-15.27 (Pass)
fed_report: docs/agents/reports/PROP-15.27-FED01-to-PM01.md
slop_report: docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Smoke — RepoRoot display redact in Presence/Settings notices
---

# PROP-15.27b — QA smoke after 15.27

## Prereqs

- Current ChatDesktop Release from this tree (15.27 bits)
- Host feel-drive pin may stay up; this is display-only

## Checks

1. Trigger local-stack start (or Settings local-stack progress) so a RepoRoot notice/progress line appears.
2. User-visible string must show `%USERPROFILE%\…` (or equivalent redact), **not** `C:\Users\<machine-username>\…`.
3. Stack still starts / resolves real path (functional — not a rename of cwd).
4. Currency chrome still `Personas OK` if Host 0.2.0 is up (spot).

## Report

`docs/agents/reports/PROP-15.27b-QA01-to-PM01.md` — on Pass note `Ready for SLOP-01`.
