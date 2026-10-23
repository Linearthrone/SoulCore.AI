---
type: report
prop_id: PROP-16.1
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.1-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.1 — Presence glass shell — Pass

## Verdict

**Pass.** Avenue B phase 1 is on `House/House.ChatDesktop` (Avalonia 11.3.18). Tray, global hotkeys, frameless drawers, light/dark chrome, and per-persona transcripts are in. Phase 2, 3, and 4 were not started. No commit, no push.

GUI smoke (clicking the live tray and drawers) was not run this session. Evidence below is the build, the unit tests, and the wiring those tests lock.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| Tray menu opens chat, opens settings, and exits | Pass | `PresenceTrayMenu` items are `Open main drawer`, `Open settings`, `Exit`. They call `ShowChatDrawer`, `ShowSettingsView`, and `ExitGlass`. Label test passed. Live right-click was not clicked. |
| `Ctrl+Shift+H` shows and hides the chat drawer with the tray icon absent | Pass | `PresenceHotkeyHost` registers the chord on a message-only window, independent of the tray. `Win32Hotkeys_RegisterWhenTheChordIsFree` passed on this machine (`RegisterHotKey` returned true). A `WM_HOTKEY` was not injected. If registration fails, `ShowInTaskbar` stays true and the focused window still handles the chord. |
| `Ctrl+Shift+Q` exits with the tray icon absent | Pass | Same host registers `Ctrl+Shift+Q` as `PresenceHotkeyAction.Exit`. Exit sets `GlassShellState.IsExiting`, posts `allowComputerControl: false`, then shuts down. Drawer close is cancelled until that flag is set, so a drawer close cannot quit. |
| No taskbar button while those hotkeys work | Pass | `ShowInTaskbar = false` runs only after `TryRegister` succeeds (`HideFromTaskbar` test). Drawers and the radial are also `ShowInTaskbar = false`. |
| Radial offers Create Agent, Contacts, and at most six favorites | Pass | `LeftEdgeRadial` draws Create agent, Contacts, then favorites. `FavoriteRoster` rejects a seventh slot and a duplicate display name. Test passed. Names on the arc are clipped to 14 characters. |
| Light and dark both render; chrome opacity cannot go below ~0.55 | Pass | Settings → System has Dark / Light and a slider with `Minimum="0.55"`. `GlassChrome.ClampOpacity(0.2)` is `0.55`. Shell alpha uses that floor; text alpha stays 255. `LocalUiSettings.Normalize` persists the floor. Brushes were not screenshotted. |
| A second persona does not read the first persona’s local transcript | Pass | `ChatTranscriptKey.ForPersona` writes `persona:{id}`. `SecondPersona_DoesNotReadFirstTranscript` saved `alpha-secret` and `beta-secret` and each load returned only its own row. `presence-local` is rejected on save. Old shared rows stay in the file and are not copied onto a persona. |
| Composer, mood correction, HOUSE lamps, browser pane, desktop stills, and Settings sections still open | Pass | Composer (Send, quote, Attach, Hold to talk, Video), mood panel, update toast, and the persona wizard steps are the existing controls. Browser pane and desktop stills stay in the chat drawer. HOUSE lamps and Settings (Identity, Tools & Access, Email, System, Services, Notifications, Updates) are reparented into their own frameless windows. Not clicked in a running window. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Output below. 0 warnings, 0 errors. |
| Report lists commands and results | Pass | This file. |

## What shipped

- Tray icon (`Assets/house-victoria.ico`) with the three menu actions. If the icon fails to load, the icon stays hidden and the hotkeys remain the way out.
- `Ctrl+Shift+H` toggles the chat drawer. `Ctrl+Shift+Q` exits. Both are Win32 `RegisterHotKey` on a message-only window.
- After those hotkeys register, the process leaves the taskbar and the chat drawer starts hidden. The left-edge half-circle stays up. It grows to 340×360 (about 4× the 46×96 tab).
- Separate frameless windows for chat (the existing main window), settings, creator (persona wizard, now frameless), contacts, HOUSE lamps, and the radial. Hide, drag-away (window mostly off a working area), or the HOUSE tab dismisses a drawer. Close does not quit. Alt+F4 on a drawer is cancelled the same way.
- Chat drawer lists threads with each persona’s name and a circular portrait (favorite photo, otherwise initials). Mode chip is ONE. Many is not implemented.
- Light and dark brush sets. Chrome opacity floor 0.55, applied to chrome fills, not to text.
- `presence-chat.db` rows are keyed by persona id.
- CUA clear on Exit, on `ProcessExit` / unhandled exception, and immediately before Velopack `ApplyUpdatesAndRestart`. One POST per process, 800 ms timeout, loopback only. Not checked against a live Host.

## Out of scope (not started)

Live VM viewport, clickthrough, view opacity, trait catalog, physical-description AI help, chat-multi, Host contract changes.

## Limits QA should know

- Host persona packs have no portrait field. A favorite photo is a local file from Contacts → Set photo. Otherwise the circle shows initials, not a symbol.
- Legacy `presence-local` rows are not migrated. They are not shown to any persona.
- Favorite display names must be unique. A seventh pin is refused.
- The radial blocks its window rectangle while open. Click-through is 16.2.
- CUA clear is best-effort. A dead Host does not block exit past the timeout.
- No running Presence window was driven this session.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~LocalUiSettingsLayoutTests|FullyQualifiedName~GlassShellTests"
```

### Build

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:00.96
```

### Tests

```text
Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 122 ms - House.ChatDesktop.Tests.dll (net8.0)
```

That run is `GlassShellTests` (9) plus `LocalUiSettingsLayoutTests` (2). The hotkey test registered `Ctrl+Shift+H` and `Ctrl+Shift+Q` and then released them.

## Key files

| File | Role |
| --- | --- |
| `House/House.ChatDesktop/MainWindow.Glass.cs` | Tray, hotkeys, drawer host, theme, exit |
| `House/House.ChatDesktop/Controls/LeftEdgeRadial.cs` | Custom-drawn radial |
| `House/House.ChatDesktop/Drawers/RadialEdgeWindow.cs` | Left-edge tab window |
| `House/House.ChatDesktop/Drawers/ContactsDrawerWindow.cs` | Contacts drawer |
| `House/House.ChatDesktop/Drawers/GlassDrawerChrome.cs` | Frameless settings and HOUSE hosts |
| `House/House.ChatDesktop/Services/ChatTranscriptKey.cs` | Persona transcript key |
| `House/House.ChatDesktop/Services/ChatHistoryStore.cs` | Rejects shared `presence-local` writes |
| `House/House.ChatDesktop/Services/PresenceHotkeyPlan.cs` | Chord and tray labels |
| `House/House.ChatDesktop/Services/PresenceHotkeyHost.cs` | Win32 hotkey window |
| `House/House.ChatDesktop/Services/GlassChrome.cs` | Light/dark brushes, opacity floor |
| `House/House.ChatDesktop/Services/FavoriteRoster.cs` | Six unique favorites |
| `House/House.ChatDesktop/Services/CuaLampClear.cs` | CUA off on exit / crash / update restart |
| `House/House.ChatDesktop.Tests/GlassShellTests.cs` | Transcript, hotkey, tray, favorites, opacity |
