---
type: report
prop_id: PROP-12.1
prop_root: PROP-12-presence-resizable-shell
from: FED-01
to: PM-01
priority: P1
status: Partial
created: 2026-09-16
branch: cursor/presence-resizable-panes-9531
environment: Linux cloud agent (TINA-main tree)
proposal: docs/agents/unexecuted_proposals/presence-resizable-shell.md
intake: docs/agents/tasks/PROP-12.1-PM01-to-FED01.md
---

# PROP-12.1 — Presence resize + pane splitters (FED-01 → PM-01)

**Verdict: Partial** — frameless resize, maximize, GridSplitters, and layout persistence are implemented and build/unit-tested. Full drag/maximize visual QA needs Kurt's Windows Presence box (ChatDesktop is WinExe).

## What shipped

| Requirement | Status | Evidence |
| --- | --- | --- |
| Edge/corner resize via `BeginResizeDrag` | Done | `MainWindow.axaml` resize grips + `MainWindow.Layout.cs` |
| Grips disabled when maximized | Done | `SyncResizeGripState` |
| Maximize / restore chrome button | Done | `MaximizeButton` + `ToggleMaximize` |
| Double-click title strip toggles maximize | Done | `TitleDragRegion_DoubleTapped` |
| No move-drag when maximized | Done | `TitleDragRegion_PointerPressed` guard |
| Chat \| right column `GridSplitter` | Done | `PresenceColumnSplitter` |
| Her browser \| What she saw `GridSplitter` | Done | `PresenceRowSplitter`; sight no longer fixed-only |
| Min sizes for panes / window | Done | `LocalUiSettings` mins + Grid MinWidth/MinHeight |
| Persist W/H/X/Y/maximized + side + sight | Done | `LocalUiSettings` + debounced save / close |
| Settings view untouched | Done | No Settings layout rewrite |

## Cloud verification

```
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release
Build succeeded. 0 Error(s)

dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Release --filter FullyQualifiedName~LocalUiSettingsLayout
Passed!  - Failed: 0, Passed: 2
```

## Partial — why not Pass

1. No GUI walkthrough on this Linux agent — Presence shell does not run as a full Windows frameless desktop here.
2. Kurt should smoke: edge resize, maximize/restore, both splitters, restart and confirm `%LocalAppData%\HouseVictoria\ui-settings.json` restores sizes.

## Out of scope (per ticket)

- Re-enabling OS decorations
- House drawer / materials redesign (PROP-4)
- Host API changes

## Recommended next

| Owner | Action |
| --- | --- |
| QA-01 / Kurt | Windows Presence: resize edges, maximize, drag both splitters, restart restore |
| PM-01 | Accept → Pass when Windows smoke is green |
