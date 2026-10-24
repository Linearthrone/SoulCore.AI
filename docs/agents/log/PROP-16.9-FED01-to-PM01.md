---
type: report
prop_id: PROP-16.9
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Ready for QA-01
created: 2026-10-23
updated: 2026-10-23
ticket: docs/agents/tasks/PROP-16.9-PM01-to-FED01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.9 — Trait hovers and Open chat — FED verification Pass

PM accept 2026-10-23: Pass (client) on the ticketed checks. After this report, the live window still vanished. `Glass_Opened` called `HideChatDrawer` on every `Show`, because Avalonia raises `Opened` again after `Hide`. The startup log recorded `MainWindow opened` on each click, then the drawer was parked. That hide now runs once, on the first open. Presence was restarted with that change. No live click was done.

## Verdict

**Pass** on the FED checks below. Ready for QA-01. No commit. No push. Branch stayed `pr-177`. The running Presence process and the Host were not stopped.

Every catalog trait now carries operator hover text: one sentence on what the slider changes, a low-end behavior example, and a high-end behavior example. The wizard sets that bubble on the slider and its label for every group, including More traits. Compiler Low/Mid/High lines and the eight-line cap are unchanged. Contacts **Open chat** on the already-foreground persona shows the chat drawer and does not call persona activate. A cancelled quarantine confirm does not switch and does not open the drawer. Showing the drawer moves it back onto the work area when it is fully off, then raises it in front of the topmost radial and the VM tab.

No live wizard hover or Contacts click was performed. The running window was left up.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| All 50 traits have a hover with a description, a low example, and a high example | Pass | `All_50_traits_have_a_description_and_both_examples`. `PersonaTraitCatalog.All` is 50. Each row has a one-sentence `HoverSummary`, a `HoverLow` example, and a `HoverHigh` example. `HoverText` contains all three, with `Low:` and `High:` labels. The strings are operator copy. Warmth and boundary compiler lines are unchanged. `PersonaTraitCompiler.MaxDirectiveLines` is 8. `PersonaTraitCompilerTests` passed 16 in Release. |
| The bubble is on the slider and the label in every wizard group | Pass | `BuildTraitSliders` walks `PersonaWizardLogic.TraitGroups`, including More traits, and each row is `CreateTraitRow`. That method sets `ToolTip.SetTip` to `trait.HoverText` on the label and on the slider. The same test walks every group, including the More traits group, and checks the bubble text. Not hovered in a live window. |
| Open chat on the already-foreground persona shows the drawer and does not call activate | Pass | `OpenChat_on_the_foreground_persona_shows_the_drawer_without_activate`. `ContactsOpenChat.Resolve(alreadyForeground: true, …)` is `ShowDrawer` true and `Activate` false. `OpenPersonaFromContactsAsync` uses that plan, calls `OpenThreadWithoutActivating` with the drawer on, and returns before `SwitchToPersonaAsync`. Persona `ActivateAsync` is not called. `ShowChatDrawer` still calls `Window.Activate` so the chat window comes forward. |
| A cancelled quarantine confirm does not show the drawer and does not switch | Pass | `OpenChat_cancelled_confirm_does_not_show_the_drawer_or_switch`. `Resolve(alreadyForeground: false, quarantineConfirmed: false)` is `ShowDrawer` false and `Activate` false. `SwitchToPersonaAsync` uses that plan, restores the previous switcher item, and returns before `ActivateAsync` and before `ShowChatDrawer`. |
| The chat drawer is in front of the radial and the VM tab | Pass | `ShowChat_raises_chat_last_so_it_paints_in_front_of_the_radial_and_vm_tab`. `ChatDrawerFront.TopmostRaiseOrder` is radial, then VM tab, then chat. `ShowChatDrawer` re-enters that topmost band in that order (`RadialEdgeWindow.ReenterTopmost`, `ViewportDrawer.ReenterTabTopmost`, then the chat window) before `Show`. `Off_work_area_moves_back_on_screen_and_an_on_screen_window_stays`: a window at `(-4000, -4000)` is centered on the work area; a window already on the work area is left where it is. `ShowChatDrawer` runs that move before `Show` when a work area is known. Not shown in a live window. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Build succeeded. 0 warnings, 0 errors. Output below. No file lock on this project. |
| Report lists commands and results | Pass | This file. |

## What changed

- Each `PersonaTraitDefinition` has `HoverSummary`, `HoverLow`, `HoverHigh`, and `HoverText`. The wizard tooltip uses `HoverText`. Compiler `Low` / `Mid` / `High` are the same strings, and `MaxDirectiveLines` stays 8.
- Contacts Open chat on the foreground persona shows the drawer through `ContactsOpenChat` and does not activate that persona. A cancelled confirm restores the switcher and does not open the drawer.
- `ShowChatDrawer` moves a fully off-work-area window back on screen, then raises the radial, the VM tab, and the chat window in that order so the chat paints in front. Hiding the drawer clears that topmost flag unless the operator's always-on-top toggle is on.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~Prop169HoverAndOpenChatTests"
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~PersonaTraitCompilerTests"
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

Time Elapsed 00:00:01.20
```

### Hover, Open chat, and drawer placement

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Core -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll
  House.ChatDesktop.Tests -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
Test run for C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 96 ms - House.ChatDesktop.Tests.dll (net8.0)
```

### Compiler lines and the eight-line cap

Debug `dotnet test` of `SoulCore.Protocol.Tests` could not copy into `SoulCore.Host\bin\Debug` because `.NET Host (36796)` had those files locked. That process was left running. The same filter was run in Release.

```text
Passed!  - Failed:     0, Passed:    16, Skipped:     0, Total:    16, Duration: 136 ms - SoulCore.Protocol.Tests.dll (net8.0)
```

## Notes for PM

- Ready for QA-01. A live hover on a trait slider, including More traits, and a live Contacts Open chat (foreground persona, and a cancelled quarantine confirm) were not clicked.
- The Host lock above is a note. The operator session was not stopped.
