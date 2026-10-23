---
type: report
prop_id: PROP-16.2
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Stop
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.2-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.2 — Live viewport drawer — Stop

## Verdict

**Stop.** The quarter-screen drawer, clickthrough toggle, view-opacity slider, and consent pause shipped on `pr-177`. The live VirtualBox guest was **not** parented.

`SetParent` + `WS_CHILD` + `MoveWindow` (the current `VictoriaBrowserEmbedHost` sequence) resizes the child window. It does not scale a fixed framebuffer, and the click arrives at the resized window coordinate, not at the pre-scale guest pixel. Parenting that HWND into the quarter drawer would be a dishonest hit-test. No frosted stand-in was shipped. No commit, no push.

A running Presence window was not clicked this session. Evidence is the build, the unit tests, and the Win32 probe below.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| Viewport drawer opens top-left at about one quarter of the work area | Pass | `ViewportLayout.OfWorkArea(10, 20, 1920, 1080)` is `(10, 20, 960, 540)`. `ViewportDrawer.Pin` places the top-edge strip and the guest window inside that rect. Not clicked in a live window. |
| Guest picture is not frosted; chrome opacity does not fade it | Stop | The guest HWND is not parented, so there is no live picture to accept. The guest window uses `WindowTransparencyLevel.None`, an opaque fill, and `Opacity = 1`. It does not use acrylic. Chrome opacity is not applied to a guest HWND. This line is not Pass. |
| Clickthrough defaults off and applies only to this drawer | Pass | `ViewportInputPolicy` starts with clickthrough false. `Clickthrough_DefaultsOff_AndDialogsForceItOffAndPauseCua` passed. The extended style is changed only on the viewport guest window, and only after the guest is detached. Other drawers are unchanged. |
| View opacity does not change the chrome opacity slider | Pass | `ViewOpacity_DoesNotChangeChromeOpacity` passed. View opacity 0.3 leaves chrome at 0.82. Chrome floor 0.55 does not clamp view opacity. `GuestLayerAlpha(1)` is null, so a full view does not put a layered style on the guest. The view slider writes `LocalUiSettings.ViewOpacity` only. |
| An open confirm or update dialog forces clickthrough off | Pass | `Open` for tool confirm, quarantine confirm, and update apply clears clickthrough and sets `CuaPaused`. The same test passed. The persona-switch dialog and the new update-apply dialog call `Open` while they are showing. While paused, if computer control was on, Presence posts `allowComputerControl: false` and restores it when the dialog closes. That POST was not checked against a live Host. |
| Browser pane and desktop stills still open from the chat drawer | Pass | Her screen and What she saw stay in the chat drawer. A VM hwnd is offered to the viewport and is not `SetParent`'d into the chat window. Playwright embed in the chat pane is unchanged. The Live view button reopens the viewport. Not clicked in a live window. |
| If scale or hit-test fails, the report says stop and does not claim Pass | Pass | This file. The live-guest line above is Stop, not Pass. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Output below. 0 warnings, 0 errors. |
| Report lists commands and results | Pass | This file. |

## Why the guest stopped

Probe `GuestEmbedFit.ProbeQuarterScale` uses the same `SetParent`, `WS_CHILD`, and `MoveWindow` sequence as `VictoriaBrowserEmbedHost`. A 400×300 framebuffer was resized into a 200×150 slot. The pixel that would be `(300, 200)` on a scaled guest sits at window `(150, 100)`. The click was delivered at `(150, 100)`.

```text
EmbedProbeResult { Ran = True, HostWasClickThrough = False, WindowResizedToSlot = True, FrameX = 300, FrameY = 200, ClickX = 150, ClickY = 100, ReceivedX = 150, ReceivedY = 100, ScaledClickHonest = False }
SetParent+MoveWindow resized the child to 200x150. Framebuffer pixel (300,200) would appear at (150,100) if the guest scaled. The click was delivered at (150,100), which is the window coordinate, not the framebuffer pixel. Host was not click-through. Guest was not parented into a frosted window.
```

`SetParent_DoesNotMapClickOntoScaledFramebuffer` passed on those asserts. `GuestEmbedFit.Decide` therefore returns `ScaleStop` when the guest frame size is not the slot size. The viewport shows that stop text on an opaque window. It does not parent the HWND, and it does not paint a frosted or JPEG stand-in.

A 1:1 frame (frame size equal to the slot) is the only `Decide` allow. After `SetParent`, `GuestWindowFillsHost` and `HitTestReachesGuest` must both succeed or the guest is detached. A normal VM framebuffer is larger than a quarter of the work area, so that allow does not cover the locked quarter-screen guest. No VirtualBox window was parented in this session.

## What shipped

- Top-left viewport drawer, about one quarter of the work area. Top edge (36 DIP inside that quarter) has Clickthrough, a View slider, and Hide. Guest area has a 1px border on an opaque window. No acrylic, no window opacity, no click-through while a guest is parented.
- Clickthrough default off. Turning it on detaches any guest first, then sets `WS_EX_TRANSPARENT` on the empty guest window only. The top-edge strip stays hittable.
- View opacity is its own setting (`LocalUiSettings.ViewOpacity`, default 1). It does not write `ChromeOpacity`. At 1 the guest is not given a layered style.
- Quarantine confirm and update apply open opaque, topmost dialogs. While either is open, clickthrough is forced off and CUA input is paused. Tool confirm uses the same `ViewportInputPolicy.Open` gate. Presence has no separate tool-confirm window; the unit test opens that kind.
- Browser pane and desktop stills remain in the chat drawer. Live view reopens the viewport.
- Out of scope and not started: trait catalog, description AI help, chat-multi, Host contract changes.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter FullyQualifiedName~ViewportDrawerTests
```

Branch during the run: `pr-177`.

### Build

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.29
```

### Tests

```text
Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 41 ms - House.ChatDesktop.Tests.dll (net8.0)
```

The seven tests are clickthrough default and the three dialog pauses, view-opacity independence from chrome opacity, clickthrough detach-before-enable, quarter placement, frost/transparency refusal, the scale stop decision, and the Win32 probe above.

## Key files

| File | Role |
| --- | --- |
| `House/House.ChatDesktop/Drawers/ViewportDrawer.cs` | Quarter drawer, top-edge controls, parent gate |
| `House/House.ChatDesktop/Services/ViewportInputPolicy.cs` | Clickthrough default, view opacity, dialog pause |
| `House/House.ChatDesktop/Services/GuestEmbedFit.cs` | Scale/hit-test decision and Win32 probe |
| `House/House.ChatDesktop/Controls/VictoriaBrowserEmbedHost.cs` | Fill and hit-test checks after a 1:1 parent |
| `House/House.ChatDesktop/MainWindow.BrowserView.cs` | VM hwnd goes to the viewport; chat pane keeps JPEG |
| `House/House.ChatDesktop/MainWindow.Glass.cs` | Show the drawer; pause CUA while a dialog is open |
| `House/House.ChatDesktop.Tests/ViewportDrawerTests.cs` | The seven tests |
