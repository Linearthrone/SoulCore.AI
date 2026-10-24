---
type: report
prop_id: PROP-17
prop_root: PROP-17-right-chat-and-live-vm
from: FED-01
to: PM-01
priority: P0
status: Ready for QA-01
created: 2026-10-23
updated: 2026-10-23
ticket: docs/agents/tasks/PROP-17-PM01-to-FED01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
operator: LinearThrone
---

# PROP-17 — Right-edge chat drawer and a live VM picture — FED verification Pass

## Verdict

**Pass.** Ready for QA-01. No commit. No push. Branch stayed `pr-177`. The running Presence process was left on `bin\Release`. The Host listeners on 7720 and 7700 were not stopped. `ALLSTOP.ps1` was not run. No stash was popped. Local `main` was not touched.

Build configuration: **Debug** (the project default). `bin\Release` was left alone because Presence is running from that folder. The Debug build did not need a side output directory.

## What the operator will see

The window that is open now is the previous Release process. It was not restarted, so it does not show this build. The next start of the Debug client shows:

- A glass **Chat** tab on the right edge of the work area, vertically centered, 36×120 DIP. The fill is the shared radial glass (alpha 168, 206, 228, 255) and the stroke is `#4BA3FF`. The tab stays on screen. The transcript is hidden.
- A click, or a drag toward the center of at least 16px, opens that same window as the right half of the work area. The body hangs to the left of the tab. The tab stays on the right edge as the header. Transcript, composer, threads, and the existing controls are that body. There is no second chat window.
- Hide, the window close button, and Ctrl+Shift+H while open return to the tab. They do not quit. Ctrl+Shift+Q still quits.
- Ctrl+Shift+H from the tab, the tray item, Contacts **Open chat**, and a radial favorite open the right half and leave it open, in front of the radial and the VM tab.
- The VM tab stays on the top edge. The radial stays on the left. While guest JPEG frames arrive, the picture well draws each new frame and the caption reads **Live guest**. With no frame yet, the well reads **The guest window is not ready.** If those JPEG bytes never arrive and a VirtualBox window handle is known, the client copies that window with PrintWindow or BitBlt at about 5 fps into the same still. The VirtualBox window is not parented, not moved, and not clicked. The well stays opaque, unfrosted, and not click-through.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| A new VM JPEG is drawn in the VM drawer picture well and replaces the previous frame | Pass | `ShowVmFrame` calls `ViewportDrawer.ShowGuestJpeg`. That stores the bytes on `ViewportStillSlot` and, while the VM drawer is open, decodes them and calls `ShowStill`, which sets `Image.Source` to the new bitmap and drops the previous one. `NewFrame_ReplacesThePreviousStill_AndTheCaptionIsLive`: the second frame's bytes replace the first (`Generation` 1 then 2, `Current` is the second buffer, `Previous` is the first). A desktop capture is drawn the same way only when a guest window is known and VM JPEGs are not already arriving. |
| No path parents the VirtualBox HWND. `GuestEmbedFit` still stops a mismatched frame | Pass | The VM branch still calls `OfferGuest(0, …)` and does not call `ApplyVictoriaBrowserEmbed`. `GuestWindowGrab` has no `SetParent` and no `MoveWindow`. `Grab_CopiesPixels_WithoutParentingTheWindow`: parent before and after the copy were equal, and the copy was a BMP. `GuestEmbedFit.Decide(960, 540, 1920, 1080, …)` is still `ScaleStop`. `ViewportStill.MayParent` is false. `GuestEmbedFit.cs` was not edited. |
| The picture well stays opaque, unfrosted, and not click-through. The live caption is not the old still label | Pass | `ShowStill` still calls `KeepStillOpaque` (`WindowTransparencyLevel.None`, opacity 1). `ViewportStill.Frosted` is false, `AllowsClickthroughOnStill` is false, `WindowOpacity` is 1, fill alpha is 255. The drawer caption is `Live guest`. The test asserts that string does not contain `not a live guest`. `ViewportStill.Label` is unchanged and is not the drawer caption. |
| With no frame, the drawer says the guest is not ready | Pass | Empty text and the caption start as `The guest window is not ready.` `NoteGuestNotReady` sets that line when no frame is held. The test asserts the slot caption contains `not ready` and `guest` before the first frame, and again after `Clear`. |
| Closed chat is a right-edge tab, vertically centered, using `RadialTabChrome`, and it stays on screen | Pass | `ClosedTab_SitsOnTheRightEdge_AndOpenBodyIsTheRightHalf`: on a 10,20,1920×1080 area at scale 1 the closed tab is 36×120 at x=1894, y=500 (vertical center 560, the work-area center). `TabStaysVisible` is true and body width is 0. `InstallChatEdgeChrome` uses `RadialTabChrome` fill, stroke, and a 10 DIP corner on the inner edge. `RadialTabChrome` colors were not edited: alpha 168, rgb 206,228,255, stroke `#4BA3FF`. `HideChatDrawer` calls `SetChatOpen(false)` and does not call `Window.Hide()`. |
| Click or a leftward drag of at least 16px opens the chat body as the right half of the work area, attached to that tab | Pass | Same test: open body width is 960 and height is 1080. `BodyX + BodyWidthPx == TabX`. The tab's right edge is the work-area right edge. `DragPullsOpen(-16)` is true, `DragPullsOpen(-15)` is false, `DragPullsOpen(16)` is false. The tab pointer handler calls `SetChatOpen(true)` on that drag, and a click with no drag toggles. |
| Ctrl+Shift+H, the tray item, and Contacts Open chat open that drawer and leave it open | Pass | Ctrl+Shift+H still maps to `ToggleChatDrawer`. From the closed tab that calls `ShowChatDrawer`, which calls `SetChatOpen(true)` and does not hide (`ChatDrawerPolicy.ShouldHideOnOpen()` is false). The tray item calls `ShowChatDrawer`. Contacts Open chat and a radial favorite still reach `ShowChatDrawer`. Raise order is radial, then the VM tab, then chat. `FirstLaunch_IsTheClosedTab_AndClosingDoesNotHideOrQuit` locks those flags. Hotkey tests in the 30-test run still passed. |
| Closing the drawer leaves the tab. It does not quit | Pass | `Glass_Closing` cancels and calls `HideChatDrawer`. That returns to the closed tab. `ShouldQuitWhenClosed()` is false. `ShouldHideWindowWhenClosed()` is false. Ctrl+Shift+Q still calls `ExitGlass`. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Debug build succeeded. 0 warnings, 0 errors. Output below. `bin\Release` was not the output, because Presence is running from there. No side directory was required. |
| Tests cover the right-edge placement (closed tab, open half, drag threshold) and that a new frame replaces the still. Paste the command output | Pass | Four PROP-17 tests, all passed. Output below. A wider run of the chrome, viewport, hotkey, and Open chat tests passed 30. |
| Report lists commands and results | Pass | This file. |

## Commands

```text
git branch --show-current
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~Prop17RightChatAndLiveVmTests|FullyQualifiedName~ViewportDrawerTests|FullyQualifiedName~Prop167ChromeTests|FullyQualifiedName~Prop169HoverAndOpenChatTests|FullyQualifiedName~GlassShellTests"
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~Prop17RightChatAndLiveVmTests" --logger "console;verbosity=detailed"
```

Branch during the run: `pr-177`.

The build log prints absolute paths. The home folder name is omitted below. The rest of each line is the tool output.

### Build (Debug)

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> [repo]\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  SoulCore.Core -> [repo]\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  House.ChatDesktop -> [repo]\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:23.87
```

### PROP-17 tests (detailed)

```text
  Passed House.ChatDesktop.Tests.Prop17RightChatAndLiveVmTests.NewFrame_ReplacesThePreviousStill_AndTheCaptionIsLive [13 ms]
  Passed House.ChatDesktop.Tests.Prop17RightChatAndLiveVmTests.Grab_CopiesPixels_WithoutParentingTheWindow [30 ms]
  Passed House.ChatDesktop.Tests.Prop17RightChatAndLiveVmTests.ClosedTab_SitsOnTheRightEdge_AndOpenBodyIsTheRightHalf [< 1 ms]
  Passed House.ChatDesktop.Tests.Prop17RightChatAndLiveVmTests.FirstLaunch_IsTheClosedTab_AndClosingDoesNotHideOrQuit [< 1 ms]

Test Run Successful.
Total tests: 4
     Passed: 4
 Total time: 0.5729 Seconds
```

### Related desktop tests

```text
Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30, Duration: 752 ms - House.ChatDesktop.Tests.dll (net8.0)
```

## What changed

- VM surface JPEG bytes go to `ViewportDrawer.ShowGuestJpeg`, which replaces the previous still and draws the new bitmap with `ShowStill` while the VM drawer is open. The caption is `Live guest`. With no frame, the line is `The guest window is not ready.`
- If those bytes are missing and the guest window handle is known, `GuestWindowGrab` copies that window at 5 fps with PrintWindow, then BitBlt. It does not parent, move, or click the window. Closing the VM drawer may pause that copy. Opening it does not stop the browser-view poll.
- The one chat window is a right-edge tab when closed and the right half of the work area when open. First launch uses that tab, not the saved floating rect, and does not call `Window.Hide()` on the only window.
- `GuestEmbedFit` stop rules and `RadialTabChrome` colors were not changed. Trait compiler lines, the port block, and the running Hosts were not touched.
