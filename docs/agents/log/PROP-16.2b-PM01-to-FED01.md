---
type: task
prop_id: PROP-16.2b
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.2b-FED01-to-PM01.md
pm_note: Accepted 2026-10-22 as Pass (client). The still is opaque and unlabeled as a live guest. Live view does not parent an HWND. 8 viewport tests passed. A running window was not clicked.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.2 (Stop)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Viewport fallback — opaque still, no scaled guest
---

# PROP-16.2b — Opaque island for the viewport

## Problem

PROP-16.2 stopped. `SetParent` plus `MoveWindow` resizes the child window and does not map a click onto the pre-scale framebuffer pixel. The locked fallback is Avenue A for the viewport only: an opaque island inside the chat window. The Avenue B drawers from 16.1 stay.

## Root

`GuestEmbedFit.Decide` already returns `ScaleStop` when the guest frame is not the slot size. A normal VM framebuffer is larger than a quarter of the work area, so the 1:1 allow does not cover this lock. The chat drawer already has Her screen and What she saw (`MainWindow.axaml`). That still is the picture. It is not a live scaled guest.

## Solution

1. Do not parent a VirtualBox HWND when the frame size differs from the slot. Leave `GuestEmbedFit` on `ScaleStop`. Do not retry a different scale trick in this ticket.
2. Make What she saw the viewport picture inside the chat drawer: opaque, no acrylic, window opacity 1, thin border. The label must say it is a still, not a live guest.
3. The quarter viewport drawer does not host an HWND. Live view focuses that still. The drawer may show the same still plus the existing stop line. It does not paint a frosted or JPEG fake of a live desktop over a child window.
4. Clickthrough stays default off. Do not set `WS_EX_TRANSPARENT` on the chat drawer or on any window that shows the still.
5. Her screen (browser pane) stays reachable in the chat drawer.

## Do not

- Parent the guest to fake a scaled live view.
- Frost the still or apply chrome opacity to it.
- Collapse chat, settings, contacts, HOUSE, or the radial back into one window.
- Trait catalog, description AI help, chat-multi, or Host contract changes.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/MainWindow.axaml`
- `House/House.ChatDesktop/MainWindow.BrowserView.cs`
- `House/House.ChatDesktop/Drawers/ViewportDrawer.cs`
- `House/House.ChatDesktop/Services/GuestEmbedFit.cs`
- `House/House.ChatDesktop.Tests/ViewportDrawerTests.cs`

## Acceptance

- [ ] A mismatched guest frame is not `SetParent`'d (existing scale-stop test still passes)
- [ ] What she saw is opaque, not frosted, and labeled as a still
- [ ] Live view opens that still in the chat drawer and does not parent an HWND
- [ ] Her screen still opens from the chat drawer
- [ ] Clickthrough stays off and is not applied to the still
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] The report does not claim a live scaled guest
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.2b-FED01-to-PM01.md`
