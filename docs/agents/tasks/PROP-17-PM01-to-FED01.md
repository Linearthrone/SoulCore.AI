---
type: task
prop_id: PROP-17
prop_root: PROP-17-right-chat-and-live-vm
from: PM-01
to: FED-01
priority: P0
status: In Progress
created: 2026-10-23
updated: 2026-10-23
depends_on: PROP-16.9 (Pass, client)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
title: Right-edge chat drawer and a live VM picture
---

# PROP-17 — Right-edge chat drawer and a live VM picture

## Problem

LinearThrone wants two things, in this order.

1. The VM drawer must show a live picture of the guest as soon as frames exist. Today `ViewportDrawer.OfferGuest` discards the HWND and the picture well stays on the PROP-16.2b label "What she saw · still, not a live guest".
2. The chat window must be a drawer that pulls from the right edge and uses the same glass as the radial and the VM tab. Today chat is the full `MainWindow`, parked with `Hide()` after the first open.

## Root

`GuestEmbedFit.Decide` returns `ScaleStop` when the guest frame size is not the slot size. PROP-16.2 proved `SetParent` + `MoveWindow` resizes the child and delivers the click at the window coordinate, not the pre-scale framebuffer pixel. That stop stays. Do not parent the VirtualBox HWND into the drawer. Do not frost the guest. Do not turn click-through on.

The client already receives JPEG bytes for the VM surface (`MainWindow.BrowserView.cs` `snap.ImageBytes`, and the desktop capture in `MainWindow.Desktop.cs`). Those bytes are not drawn in the VM drawer. `ViewportDrawer.ShowStill` already accepts a bitmap and is unused by that path.

`RadialTabChrome` is the shared fill (alpha 168, 206, 228, 255), stroke `#4BA3FF`, and dark mark. `ViewportEdgeLayout` places a 120×36 tab on the top edge; a downward drag of at least 16px opens a quarter-height body under that tab. The radial stays on the left.

`Glass_Opened` parks the chat only on the first `Opened` (`_chatParkedAtStartup`). A later `Show` must stay visible. After this ticket, "closed" means the right-edge tab is on screen, not that the window is `Hide()`n.

## Solution

### Live VM picture (do this first)

- Keep `GuestEmbedFit` as it is. A mismatched frame still must not parent an HWND.
- When a new VM frame arrives (browser-view JPEG for surface `vm`, or the desktop capture JPEG when that is the guest picture), decode it and call `ViewportDrawer.ShowStill`. Replace the previous bitmap. The picture well stays opaque (`WindowTransparencyLevel.None`, opacity 1). No frost. Click-through stays off.
- While frames are arriving, the drawer caption is a live-guest line, not "still, not a live guest". When no frame has arrived, the empty line says the guest window is not ready. Do not invent a second capture service if `PrintWindow` / `BitBlt` of the existing VirtualBox window is the only way to get pixels. If you use that, leave the VirtualBox window where it is, do not `SetParent` it, and do not inject clicks. About 5 frames a second is enough.
- Opening the VM drawer must not stop the capture. Closing it may pause the decode. The radial and the chat drawer stay usable while the picture updates.

### Chat drawer from the right

- One chat surface. Do not add a second chat window.
- Closed: a tab on the right edge of the work area, vertically centered, 36×120 DIP, same fill, stroke, and corner treatment as the VM tab (`RadialTabChrome`). The tab stays visible. The transcript is not shown.
- Open: click the tab, or drag it toward the center by at least 16px. The body hangs to the left of that tab and is the right half of the work area. The tab stays on the right edge as the header. Transcript, composer, threads, and the existing chat controls fill the body.
- Header and border use `RadialTabChrome`. Do not retint the radial. The operator already said the radial's remaining white is fine.
- Ctrl+Shift+H, the tray open item, Contacts **Open chat**, and a radial favorite open this drawer (closed tab → open body) and bring it in front of the radial and the VM tab. They do not call `Window.Hide()` on the way open.
- Closing the drawer returns to the right-edge tab. It does not quit. Ctrl+Shift+Q still quits.
- The first launch shows the closed tab, not a hidden full window and not the old saved 1600×1100 rect. A later open uses the right-half placement, not the saved off-style floating position.
- The VM tab stays on the top edge. The radial stays on the left.

## Do not

- `SetParent` the VirtualBox window into the drawer, or scale it with `MoveWindow`.
- Frost the guest or enable click-through.
- Change trait compiler lines, the eight-line cap, tool policy, or the port block (Host 7720, bridge 17920, Tailscale 7720/8453).
- Stop the Host on 7720 or the Soul_Core Host on 7700. Do not run `ALLSTOP.ps1`.
- Commit or push. Stay on `pr-177`. Do not pop the stash. Do not touch local `main`.

## Files

- `House/House.ChatDesktop/Drawers/ViewportDrawer.cs`
- `House/House.ChatDesktop/Services/ViewportStill.cs`
- `House/House.ChatDesktop/MainWindow.BrowserView.cs`
- `House/House.ChatDesktop/MainWindow.Desktop.cs`
- `House/House.ChatDesktop/MainWindow.Glass.cs`
- `House/House.ChatDesktop/MainWindow.Layout.cs`
- `House/House.ChatDesktop/Services/RadialTabChrome.cs` (use it; do not retint)
- `House/House.ChatDesktop.Tests/`

## Acceptance

- [ ] A new VM JPEG is drawn in the VM drawer picture well and replaces the previous frame
- [ ] No path parents the VirtualBox HWND. `GuestEmbedFit` still stops a mismatched frame
- [ ] The picture well stays opaque, unfrosted, and not click-through. The live caption is not the old still label
- [ ] With no frame, the drawer says the guest is not ready
- [ ] Closed chat is a right-edge tab, vertically centered, using `RadialTabChrome`, and it stays on screen
- [ ] Click or a leftward drag of at least 16px opens the chat body as the right half of the work area, attached to that tab
- [ ] Ctrl+Shift+H, the tray item, and Contacts Open chat open that drawer and leave it open
- [ ] Closing the drawer leaves the tab. It does not quit
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds. If the running Presence locks `bin\Release`, build Debug or `-o` a side directory and say which
- [ ] Tests cover the right-edge placement (closed tab, open half, drag threshold) and that a new frame replaces the still. Paste the command output
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-17-FED01-to-PM01.md`
