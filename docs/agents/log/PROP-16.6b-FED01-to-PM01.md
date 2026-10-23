---
type: report
prop_id: PROP-16.6b
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Ready for QA-01
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.6b-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.6b — One/Many chip and pinned chat send — FED verification Pass

## Verdict

**Pass** on the FED checks below. Ready for QA-01. No commit. No push. Branch stayed `pr-177`. No files under `SoulCore/` were edited.

The radial and Settings → System each host one `PersonaModeControl`. Both follow `PersonaModeBoard`. Until `GET /api/personas/mode` returns, the board stays `one` and both controls show One. A choice `POST`s `{"mode":"one"}` or `{"mode":"many"}`. A failed POST restores the previous value and the board event paints both controls again.

`SendChatAsync` puts `personaId` on `chat.send`. One sends the foreground id and does not send when the open thread is someone else. Many sends the open thread’s id. The pin’s `Activate` is false, and the send path does not call activate. Focusing a different thread in One still activates, as it does today. Focusing a background thread in Many opens that transcript and does not activate it. HOUSE and the viewport keep the foreground name.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| The radial and Settings show the same mode, and a missing GET displays One | Pass | `MissingGet_ShowsOne_OnRadialAndSettings` and `GetMany_ShowsMany_OnBothControls`. The board starts at `one` / label One before any GET. `ApplyGet(false)` leaves both surfaces on One. `ApplyGet(true, "many")` sets both to Many. `OnPersonaModeChanged` applies that value to `SystemPersonaMode` and the radial `PersonaModeControl`. |
| A failed POST leaves the previous mode on both controls | Pass | `FailedPost_RestoresPreviousMode_OnBothControls`. Choosing Many from One moves both labels to Many, then `Restore` puts both back on One. The reverse, Many back to One, restores Many on both. `ChoosePersonaModeAsync` calls `Restore` when `SetModeAsync` is not ok, and the board event updates both controls. POST body is `{"mode":"one"}` or `{"mode":"many"}` (`ModePostBody_IsOneOrMany`). |
| One mode sends the foreground `personaId`, and does not send a different open thread | Pass | `OneMode_SendsForeground_AndDoesNotSendADifferentOpenThread`. Foreground `victoria` with the same open thread, or with no separate open thread, sends `personaId` `victoria` and `Activate` is false. Open thread `iris` does not send. The frame payload is `text`, `quotedText`, `sessionId` `presence-local`, and `personaId` `victoria`. `SendCurrentAsync` returns before `SendChatAsync` when the pin says not to send. |
| Many mode sends the open thread’s `personaId` and does not activate that persona | Pass | `ManyMode_SendsOpenThread_AndDoesNotActivate`. Foreground `victoria` and open thread `iris` sends `iris`. `Activate` is false. A blank open thread does not send. The frame carries `personaId` `iris` and omits `quotedText` when there is no quote. The send path never calls `ActivateAsync`. |
| HOUSE and the viewport still name the foreground persona after a background thread is focused | Pass | `ManyBackgroundFocus_DoesNotRenameHouseOrViewport`. Foreground name Victoria stays on HOUSE (`HOUSE · Victoria`) and the viewport title. `ThreadFocus` for Many + `iris` does not activate. `OpenThreadWithoutActivating` paints `NamesAfterFocus`, which leaves the foreground strings in place. One focus of a different thread may still activate (`OneFocus_MayActivate_AndNamesStayForegroundUntilTheSwitchLands`); the names change only when `ApplyForeground` runs after that switch. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Build succeeded. 0 warnings, 0 errors. Output below. |
| Report lists commands and results | Pass | This file. |

## What changed

- `PersonaModeControl` is the One/Many pair. Settings → System hosts one under Chat mode. The expanded radial hosts the same control, bottom-right, on top of `LeftEdgeRadial`. Both call `ChoosePersonaModeAsync`.
- `GET` and `POST /api/personas/mode` live on `SoulCorePersonaClient`. A missing or failed GET displays One. A failed POST restores the previous mode on the shared board.
- `SoulCoreWsClient.SendChatAsync` includes `personaId` for the thread being sent. One uses the foreground id and refuses a different open thread. Many uses the open thread id. Sending does not activate.
- Focusing a background thread in Many loads that transcript and does not call activate. The persona switcher, and a One-mode focus of a different thread, still activate. HOUSE (`HouseForegroundName` and the drawer title) and the viewport (persona line, window title, and the in-chat still name) update only when the foreground changes.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~PersonaModeTests"
```

Branch during the run: `pr-177` (`git branch --show-current`).

### Build

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  SoulCore.Core -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.95
```

### Mode controls and send pin

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Core -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll
  House.ChatDesktop.Tests -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
Test run for C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 27 ms - House.ChatDesktop.Tests.dll (net8.0)
```
