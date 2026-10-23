---
type: task
prop_id: PROP-16.7
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass
report: docs/agents/log/PROP-16.7-FED01-to-PM01.md
pm_note: Accepted 2026-10-23 as Pass (client). Top-edge tab, radial fill alpha 168 light blue, caption hit opens the wizard in front of the radial. 3 new tests and 8 viewport tests passed. No live click. The running Presence process was left up, so this build is not on screen until the next start.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.6b (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Top-edge VM drawer, radial tint, Create agent click
---

# PROP-16.7 — Radial and VM drawer

## Problem

LinearThrone is looking at the running shell. Three things are wrong.

The VM drawer (`House/House.ChatDesktop/Drawers/ViewportDrawer.cs`) is a dark quarter window (`ViewportStill` fill `#111214`) pinned at the top-left. It should pull down from the top edge, the way the radial pulls out from the left, and its chrome should use the radial tab’s texture and color.

The collapsed radial tab (`House/House.ChatDesktop/Controls/LeftEdgeRadial.cs`) is a near-white disc at alpha 230 with a blue stroke. It should be more translucent, with a little more light blue in the fill.

Create agent does not open. The hit target is only the 36px dot. The words “Create agent” sit outside that dot, and a miss collapses the radial. The radial window is `Topmost`, so a wizard opened from it can sit behind that window. If the Host is unreachable, the only notice is a line in the chat drawer.

## Root

`LeftEdgeRadial.Render` draws the caption at `at.X + 22` and records a hit only on the dot. `OnPointerPressed` treats any other expanded click as collapse. `RadialEdgeWindow` is topmost. `OpenPersonaWizardAsync` uses `ShowDialog` on the chat window.

## Solution

1. Give the VM view a tab on the top edge of the work area. A click or drag pulls the drawer down from that edge. Closed, only the tab shows. Open, the drawer hangs from the top. Do not parent a VirtualBox window. The still stays opaque and unfrosted. The tab, border, and header use the same fill and stroke as the radial tab. The picture itself is not tinted.
2. Paint the radial tab more translucent than it is now, and mix a little light blue into the fill. Keep the “CE” mark readable. The VM tab uses that same fill and stroke after the change, so the two match.
3. Clicking the Create agent dot or the words “Create agent” opens the persona wizard in front of the radial. Contacts and favorite captions get the same hit treatment. If the Host cannot open the wizard, show that on the radial, not only in a hidden chat line.

## Do not

- Scale or parent the guest. The 16.2 stop still stands.
- Change chat-multi, traits, or Host contracts.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/Controls/LeftEdgeRadial.cs`
- `House/House.ChatDesktop/Drawers/RadialEdgeWindow.cs`
- `House/House.ChatDesktop/Drawers/ViewportDrawer.cs`
- `House/House.ChatDesktop/MainWindow.Glass.cs`
- `House/House.ChatDesktop/MainWindow.Persona.cs`
- `House/House.ChatDesktop.Tests/`

## Acceptance

- [ ] Closed, the VM view is a tab on the top edge. Opening it pulls the drawer down from that edge
- [ ] The VM tab, border, and header use the radial tab’s fill and stroke. The still is not frosted and no HWND is parented
- [ ] The radial tab is more translucent than alpha 230 and its fill reads light blue. The “CE” mark stays readable
- [ ] A hit on the words “Create agent” opens the wizard in front of the radial
- [ ] A Host failure is visible on the radial
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.7-FED01-to-PM01.md`
