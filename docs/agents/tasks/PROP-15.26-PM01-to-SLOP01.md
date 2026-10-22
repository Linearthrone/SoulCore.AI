---
type: task
prop_id: PROP-15.26
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: SLOP-01
priority: P1
status: Findings
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.25 (Pass)
qa_report: docs/agents/reports/PROP-15.25-QA01-to-PM01.md
fed_report: docs/agents/reports/PROP-15.24-FED01-to-PM01.md
prior_slop: docs/agents/reports/PROP-15.23-SLOP01-to-PM01.md
slop_report: docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Post-QA slop — 15.24 capabilities wiring + feed/naming scrub
---

# PROP-15.26 — Slop audit after 15.25 Pass

## Scope

Change surface from **PROP-15.24** (closed by 15.25 QA):

- `SoulCoreHealthClient` — `capabilities.personas` parse
- `BuildCurrencyStatus` — triple-signal Host outdated
- `MainWindow.Presence.cs` / `MainWindow.Updates.cs` — health flag + chrome
- `PresenceUpdateService` — derived `DefaultGithubApiReleasesUrl`
- Related ChatDesktop tests

Also note QA non-blocking: live “Starting local stack from {RepoRoot}…” may embed machine username in transcript — recommend disposition only (do not expand into new features).

## Do not

- Modify product code; report only
- Re-litigate closed 15.23 F1–F3 unless regressions remain

## Report

`docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md` — clean or findings with severity + recommended action.
