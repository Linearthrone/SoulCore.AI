---
type: report
prop_id: PROP-17.6
prop_root: PROP-17-victoria-between-talks
from: FED-01
to: PM-01
status: Pass
created: 2026-10-12
updated: 2026-10-12
branch: cursor/prop17-6-host-stop-ui-6372
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
task: docs/agents/tasks/PROP-17.6-PM01-to-FED01.md
environment: TINA-main
---

# PROP-17.6 — FED-01 report (Host Stop unstick)

## Verdict

**Pass** — Stop Host stays enabled while “Starting Host…”; Stop during start cancels the in-flight ensure and force-kills via `LocalStackControl.StopHostAsync` → `stop-soulcore.ps1`; start timeout/failure/cancel always clears the busy flag and activity copy.

## Delivered

1. **Stop enabled during start/restart** — `HostStackBusyPolicy.IsHostStopEnabled` keeps Stop clickable when busy tag is `host-start` or `host-restart`; Start/Restart still disable for overlap.
2. **Interrupt path** — `InterruptOrStopHostAsync` (System Stop button + SoulCore lamp hold-stop) cancels the host-action CTS, bumps generation so a superseded start cannot re-stick “Starting Host…”, then calls `_stack.StopHostAsync()`.
3. **Busy clear** — `RunServiceActionAsync` finally clears `_servicesBusy` / buttons on success, fail, timeout (`EnsureStackForChatAsync` fail detail), or `OperationCanceledException`.
4. **Process cancel** — `LocalStackControl.RunProcessAsync` kills the script process tree on cancel so a hung `start-soulcore.ps1` does not orphan after Stop.

## Acceptance

- [x] Stop clickable during “Starting Host…”
- [x] Stuck/timeout start clears busy UI; Stop force-kills Host
- [x] Report: `docs/agents/reports/PROP-17.6-FED01-to-PM01.md`

## Verify

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj  → succeeded (0 warnings)
dotnet test  House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj → passed (incl. HostStackBusyPolicyTests)
```

## Files (House/House.ChatDesktop only + docs)

- `House/House.ChatDesktop/MainWindow.Presence.cs`
- `House/House.ChatDesktop/MainWindow.axaml.cs`
- `House/House.ChatDesktop/Services/LocalStackControl.cs`
- `House/House.ChatDesktop/Services/HostStackBusyPolicy.cs`
- `House/House.ChatDesktop.Tests/HostStackBusyPolicyTests.cs`
- `docs/agents/tasks/PROP-17.6-PM01-to-FED01.md` (copied onto branch)
- `docs/agents/reports/PROP-17.6-FED01-to-PM01.md`

## Naming

- Docs/UI operator: **LinearThrone**. No Kurt.
