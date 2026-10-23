---
type: report
prop_id: PROP-16.2b
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.2b-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.2b — Opaque island for the viewport — Pass

## Verdict

**Pass.** The viewport picture is the desktop still inside the chat drawer. It is opaque, not frosted, and labeled as a still. Live view focuses that still. The quarter drawer does not parent an HWND. It can show the same still plus the scale-stop line.

This is not a live scaled guest. `SetParent` scaling was not retried. No VirtualBox window was parented. A running Presence window was not clicked. Evidence is the build and `ViewportDrawerTests` (8 passed).

`GuestEmbedFit.Decide` is unchanged. A frame that is not the slot size is still `ScaleStop`. A 1:1 frame still returns Allow from `Decide`. The drawer does not parent in either case. A normal VM framebuffer is larger than the quarter slot, so the 1:1 allow does not cover this lock.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| A mismatched guest frame is not `SetParent`'d (existing scale-stop test still passes) | Pass | `SetParent_StopsWhenTheFrameDoesNotMatchTheSlot` passed. `GuestEmbedFit.Decide(960, 540, 1920, 1080, …)` is `ScaleStop` and `Allow` is false. `ViewportStill.MayParent` is false for that pair and for a 1:1 pair. `ViewportDrawer.OfferGuest` discards the HWND. The drawer has no `SetParent`, no `SetWindowLong`, and no `VictoriaBrowserEmbedHost`. The VM path in `MainWindow.BrowserView` passes hwnd `0`. |
| What she saw is opaque, not frosted, and labeled as a still | Pass | `Still_IsNotParented_AndStaysOpaque` passed. Label is `What she saw · still, not a live guest`. Fill alpha is 255. Window opacity is 1. Border is 1 DIP. `Frosted` is false. `ApplyViewportStillChrome` removes `metal` / `glass` / `carbon` / `inset` from the still and sets that fill. The chat shell around it is unchanged. Not clicked in a live window. |
| Live view opens that still in the chat drawer and does not parent an HWND | Pass | `ViewportStill.LiveView()` focuses the still, `ParentHwnd` is false, and Her screen stays reachable. `ShowViewport_Click` calls `ShowPresenceView`, `ShowChatDrawer`, `BringIntoView`, and `Focus` on `ScreenPanel`. It does not call `OfferGuest` or `SetParent`. Not clicked in a live window. |
| Her screen still opens from the chat drawer | Pass | The Her screen panel is still in the chat drawer, above the still. Live view shows that presence surface and does not remove the browser pane. Playwright embed in the chat pane is unchanged. The VirtualBox HWND is not parented into Her screen. Not clicked in a live window. |
| Clickthrough stays off and is not applied to the still | Pass | `ViewportInputPolicy` still starts with clickthrough false. `Clickthrough_DefaultsOff_AndDialogsForceItOffAndPauseCua` passed. `ViewportStill.AllowsClickthroughOnStill` is false. `Present(…, clickthroughRequested: true)` returns `ClickthroughApplied` false. The drawer toggle forces itself back off and does not write `WS_EX_TRANSPARENT`. That style is not set on the chat drawer. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Output below. 0 warnings, 0 errors. |
| The report does not claim a live scaled guest | Pass | This file. The guest was not parented and was not scaled. |
| Report lists commands and results | Pass | This file. |

## What changed

- What she saw is the opaque island in the chat drawer: solid fill, opacity 1, 1 DIP border, no acrylic class. The title says it is a still, not a live guest. Chrome brushes are not used on that island.
- Live view brings the chat drawer forward and focuses that still.
- The quarter drawer shows the same still and, once a guest frame is offered, the existing stop line (`ScaleStop` when the frame is not the slot). It does not host an HWND and does not paint that still over a child window.
- Clickthrough stays off. View opacity can still be stored and does not fade the still. The still windows stay at opacity 1 with `WindowTransparencyLevel.None`.
- Avenue B drawers (settings, contacts, HOUSE, radial) were not collapsed. Trait catalog, description AI help, chat-multi, and Host contracts were not started.
- No commit. No push. Branch stayed `pr-177`.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter FullyQualifiedName~ViewportDrawerTests
```

Branch during the run: `pr-177` (`git branch --show-current`).

### Build

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:04.37
```

### Tests

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll
  House.ChatDesktop.Tests -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
Test run for C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 49 ms - House.ChatDesktop.Tests.dll (net8.0)
```

The eight tests are the previous seven (clickthrough default and dialog pause, view-opacity independence, clickthrough detach-before-enable, quarter placement, frost refusal, scale-stop decision, Win32 scale probe) plus `Still_IsNotParented_AndStaysOpaque`.

The Win32 probe still resizes a throwaway 400×300 window into a 200×150 slot and records that the click is the window coordinate, not the framebuffer pixel. That probe is not a parented VirtualBox guest.

## Key files

| File | Role |
| --- | --- |
| `House/House.ChatDesktop/Services/ViewportStill.cs` | Still label, opaque fill, no-parent gate |
| `House/House.ChatDesktop/Drawers/ViewportDrawer.cs` | Quarter drawer shows the still and the stop line; no HWND |
| `House/House.ChatDesktop/MainWindow.axaml` | Opaque What she saw island; Live view tooltip |
| `House/House.ChatDesktop/MainWindow.Desktop.cs` | Applies the still chrome; shares the bitmap; opaque pop-out |
| `House/House.ChatDesktop/MainWindow.Glass.cs` | Live view focuses the still |
| `House/House.ChatDesktop/MainWindow.BrowserView.cs` | VM frame is recorded; HWND is not passed |
| `House/House.ChatDesktop/Services/GuestEmbedFit.cs` | Unchanged scale stop |
| `House/House.ChatDesktop.Tests/ViewportDrawerTests.cs` | Scale stop plus the still-not-parent test |
