---
type: task
prop_id: PROP-15.24
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: FED-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.23 (findings)
slop_report: docs/agents/reports/PROP-15.23-SLOP01-to-PM01.md
findings: F1, F2, F3
pm_decision_f2: wire-capabilities-personas
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Scrub kurtw path; wire capabilities.personas; dedupe feed URLs
---

# PROP-15.24 — SLOP 15.23 cleanup

## F1 (P0 AGENTS)

`MainWindow.Presence.cs` — remove `kurtw` from system notice. Use neutral example (`%USERPROFILE%\…` or `C:\Users\<you>\…`) — never forbidden human-name tokens.

## F2 (PM decided)

Parse `/health` **`capabilities.personas`**. Treat missing/`false` as Host outdated for personas **alongside** existing persona-object + API-404 signals (any fail → outdated). Do not drop the other checks.

## F3

Dedupe `DefaultGithubApiReleasesUrl` — derive from `DefaultGithubRepoUrl` via existing `TryGithubApiFromRepoUrl`.

## Acceptance

- [ ] No kurt/kurtw/Kurt in ChatDesktop scoped paths (`rg` clean)
- [ ] Currency uses capabilities.personas
- [ ] Single feed URL owner
- [ ] Tests + report `docs/agents/reports/PROP-15.24-FED01-to-PM01.md`
