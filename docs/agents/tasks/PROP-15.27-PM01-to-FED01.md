---
type: task
prop_id: PROP-15.27
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: FED-01
priority: P2
status: Done
created: 2026-10-05
updated: 2026-10-05
fed_report: docs/agents/reports/PROP-15.27-FED01-to-PM01.md
depends_on: PROP-15.26 (findings)
slop_report: docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md
finding: F1
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Redact UserProfile prefix in user-visible RepoRoot notices
---

# PROP-15.27 — Live RepoRoot notice redact (SLOP 15.26 F1)

## Decision

**Ticket-FED.** Minimal display redact only — keep real paths for process cwd / scripts.

## Problem

`AppendSystemNotice($"Starting local stack from {_stack.RepoRoot}…")` and Settings progress `Repo: {RepoRoot}` can embed `C:\Users\<machine-username>\…` in user-visible copy (AGENTS naming).

## Fix

1. Small helper (e.g. on `LocalStackControl` or shared display util): if path starts with `Environment.GetFolderPath(SpecialFolder.UserProfile)`, rewrite that prefix to `%USERPROFILE%` for display strings only.
2. Use it in:
   - `MainWindow.Presence.cs` — “Starting local stack from …” notice
   - `LocalStackControl.cs` — `progress?.Report($"Repo: …")`
3. Do **not** change ResolveRepoRoot / script cwd / env loading.
4. Unit test(s) for the display helper.

## Report

`docs/agents/reports/PROP-15.27-FED01-to-PM01.md`
