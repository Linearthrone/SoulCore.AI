---
type: task
prop_id: PROP-16.2
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Stop
report: docs/agents/reports/PROP-16.2-FED01-to-PM01.md
pm_note: Accepted 2026-10-22 as the designed stop. SetParent resizes the child and does not map a click onto the scaled framebuffer. Drawer, clickthrough default, view opacity, and dialog pause stay. Live guest is not parented. Fallback is 16.2b, not a retry of the same parent.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.1 (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Live quarter-screen VirtualBox drawer
---

# PROP-16.2 — Live viewport drawer

## Problem

The glass shell from 16.1 has no live guest. Her screen today is stills plus an optional `SetParent` in `House/House.ChatDesktop/Controls/VictoriaBrowserEmbedHost.cs`. The accepted lock is a live VirtualBox guest, top-left, one quarter of the work area, sharp pixels, its own opacity, clickthrough default off.

## Root

Embed already uses `SetParent` into an Avalonia `NativeControlHost`. That host must not sit in a click-through or frosted window. Scaling and hit-testing are the risk called out in the proposal. This ticket hosts the existing guest. It does not re-implement PROP-14.

## Solution

1. Add a viewport drawer, top-left, about one quarter of the work area, thin glass border. Guest pixels stay sharp and opaque. Do not frost them.
2. Top edge of that drawer only: clickthrough toggle, **default off**. A separate view-opacity slider. Neither control changes chrome opacity or other drawers.
3. While a tool confirm, quarantine confirm, or update-apply dialog is open, force clickthrough off and pause CUA input.
4. Browser pane and desktop stills stay reachable in the chat drawer.
5. Reuse `VictoriaBrowserEmbedHost` / the current VM window match (`VmWindowTitle`). If `SetParent` cannot scale the guest and keep clicks honest, stop. Write the failure in the report. Do not ship a frosted fake or an opaque island unless you document that fallback as the stop condition. Do not parent the HWND into a click-through window.

## Do not

- Trait catalog, description AI help, or chat-multi (16.3–16.6).
- Clickthrough default on.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/Controls/VictoriaBrowserEmbedHost.cs`
- `House/House.ChatDesktop/MainWindow.BrowserView.cs`
- New viewport drawer beside `House/House.ChatDesktop/Drawers/`
- Tests for clickthrough default, view-opacity independence from chrome opacity, and the dialog pause rule if you extract them

## Acceptance

- [ ] Viewport drawer opens top-left at about one quarter of the work area
- [ ] Guest picture is not frosted; chrome opacity does not fade it
- [ ] Clickthrough defaults off and applies only to this drawer
- [ ] View opacity does not change the chrome opacity slider
- [ ] An open confirm or update dialog forces clickthrough off
- [ ] Browser pane and desktop stills still open from the chat drawer
- [ ] If scale or hit-test fails, the report says stop and does not claim Pass
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.2-FED01-to-PM01.md`
