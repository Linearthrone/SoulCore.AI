# PROP-22.4 — FED-01 → PM-01: glass shell, chat, composer, call scrim

**Status:** Pass (build + unit tests + Windows smoke of the main chrome).

## Architecture decisions

- `MainWindow.axaml` rewritten as a frameless 460×996 glass slab: `shell-bevel/shell/shell-rim` restyled in `App.axaml` (near-black cool glass, thin dull neon rim, Inter kept). New brushes: `NeonRimBrush`, `GlassFillBrush`, `PinkPulseBrush`, `AmberBrush` (+ `YellowBrush`, `OffDimBrush`, `GlassPanelBrush`, `GlassScrimBrush`). Icons are `StreamGeometry` resources (`IconSend`, `IconPhone`, `IconGear` …).
- One **content host** (`ContentHost`) swaps full-area screens: ChatScreen (default), Journal/Activities/Research/Bond/Tasks/Calendar/Settings/WorkItemForm/AdvancedSettings/Log. `MainWindow.Screens.cs` owns the `Screen` enum + `ShowScreen` (exhaustive `switch` with a throwing default). The old tab wall is gone.
- Top strip is a thin drag region: orb · name/mood/connection/activity · min/max/close. SETTINGS moved to the bottom-right gear; Victoria portrait button bottom-left; large round **Call** button centred **below** the composer.
- Chat: bubbles without avatars, no system rows. `AppendSystemNotice` writes to the in-memory `PerformanceLog` (Settings → Performance log); `LoadChatHistory` skips `IsSystem` rows; WS error frames also go to the log.
- Composer: growing `ChatInput` (`AcceptsReturn`, wrap, 36–140 px), round `SendButton` (path icon), secondary Attach / PTT (`live` class while recording). Quote/pending-image chips kept.
- **Call overlay** (`SetCallPhase`): *Connecting* = `RowSpan=3` scrim over the whole content area; *Connected* = `RowSpan=1` (top third) with `CallSpeakingSphere` kept; `CallButton` gets the `live` class; overlay stays up with the failure text when a call can't start (End call dismisses).
- **Aspect lock**: width-driven `Resized` handler, height = width × 19.5/9 (460 → 997 px), min 360 wide / 780 tall; persisted via `LocalUiSettings` (`CurrentLayoutVersion=2`; legacy landscape geometry is reset by `MigrateLegacyLayout`).
- Honesty: empty `currentActivity` shows **"Nothing right now"** (removed the "Resting"/"No open assignment" injections and the desktop-capture "Looking around" hint).

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
> dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj
Failed!  - Failed: 1, Passed: 242, Skipped: 0, Total: 243
  Failed: CompanionTokenEnvTests.TryLoadFromEnvFile_UsesConfiguredRepoRootOutsideBaseDirectory
          (environmental: this PC has a real saved companion token in %LOCALAPPDATA%\HouseVictoria,
           which outranks the temp .env the test writes — failed before PROP-22 too, untouched)
```

Windows smoke (this PC, isolated `%LOCALAPPDATA%`, live Host read-only on :7710): window opened `460x996.8` (log: `MainWindow opened at … size 460x996.8`), history rendered without system rows, composer/orb/call/gear/Victoria chrome visible, clean shutdown (`Avalonia lifetime ended normally`).

## Not verified / needs a human

- Call overlay phases (starting a call would open a real voice session; not exercised).
- DPI scaling other than 100%/150%; drag-resize feel at the aspect lock.
