---
type: report
prop_id: PROP-16.7
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Ready for QA-01
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.7-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.7 — Top-edge VM drawer, radial tint, Create agent click — FED verification Pass

## Verdict

**Pass** on the FED checks below. Ready for QA-01. No commit. No push. Branch stayed `pr-177`. This task did not edit `SoulCore/`. The running Presence process was not stopped. The build did not hit a file lock.

The closed VM view is a tab on the top edge of the work area. A click, or a downward drag of 16px or more, opens the drawer so it hangs from that edge. The radial tab fill is alpha 168 light blue `(206, 228, 255)` with stroke `#4BA3FF`. The VM tab, header, and drawer border use that same fill and stroke. The still window stays opacity 1, `WindowTransparencyLevel.None`, and does not parent an HWND. A hit on the words “Create agent”, “Contacts”, or a favorite caption is inside the slot hit and outside the dot. Create agent from the radial shows the persona wizard on the radial window, in front of it. A Host failure on that path is written on the radial as well as the chat line.

No live shell click was performed. The running window was left up.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| Closed, the VM view is a tab on the top edge. Opening it pulls the drawer down from that edge | Pass | `ClosedTab_SitsOnTheTopEdge_AndOpeningHangsDownFromIt`. Work area `(10, 20, 1920, 1080)`, scale 1: closed placement is not open, drawer hidden, tab at `(490, 20)` size `120×36`. Open placement keeps `TabY` at `20`, and `DrawerY` is `TabY + TabHeight`. Drawer width is the quarter width `960`. Header height plus drawer height is the quarter height `540`. A drag of `0` does not open. A drag of `16` does. `ViewportDrawer.Pin` uses `ViewportEdgeLayout.Place`. Click toggles; drag down calls `SetOpen(true)`. Not dragged in a live window. |
| The VM tab, border, and header use the radial tab’s fill and stroke. The still is not frosted and no HWND is parented | Pass | Tab and header are one chrome window: background `RadialTabChrome.FillColor`, border `RadialTabChrome.StrokeColor`. The still window border uses that same fill and stroke. The picture host and the image well use `ViewportStill.CreateFillBrush()` (alpha 255). Still window opacity is `1`, transparency hint is `None` only (no acrylic). `OfferGuest` discards the HWND. `ViewportStill.HostsGuestHwnd` is false, `Frosted` is false, `WindowOpacity` is 1. `RadialFill_IsMoreTranslucentThanAlpha230_AndLightBlue` asserts the still fill is not the radial fill. `Still_IsNotParented_AndStaysOpaque` in the existing viewport suite also passed. |
| The radial tab is more translucent than alpha 230 and its fill reads light blue. The “CE” mark stays readable | Pass | `RadialFill_IsMoreTranslucentThanAlpha230_AndLightBlue`. Previous fill was alpha 230, RGB `(247, 250, 255)`. New fill alpha is `168`, which is less than 230. Blue is ahead of green, and green is ahead of red, by more than the old blue bias of 8. Channels stay light (`R > 190`, `G > 210`, `B > 240`). The collapsed “CE” mark uses `RadialTabChrome.MarkColor` `(20, 32, 51)`, alpha 255. `LeftEdgeRadial.Render` paints the disc with `RadialTabChrome` and does not use the old alpha-230 fallback. |
| A hit on the words “Create agent” opens the wizard in front of the radial | Pass | `CaptionWords_HitOutsideTheDot_ForCreateAgentContactsAndFavorites`. For “Create agent”, “Contacts”, and “Nova”, the point in the middle of the words is inside `RadialSlotHit.For` and outside the grown dot. `LeftEdgeRadial.Render` records that rect for every slot. The radial Create agent handler calls `OpenPersonaWizardAsync(null, fromRadial: true)`. That path calls `RadialEdgeWindow.PresentAbove`, which clears the radial `Topmost` flag, sets the wizard `Topmost`, and `ShowDialog`s on the radial window so the wizard is in front. Not opened in a live window. |
| A Host failure is visible on the radial | Pass | When `fromRadial` is true and the persona list is unreachable or the persona API is missing, `OpenPersonaWizardAsync` still appends the chat line and calls `RadialEdgeWindow.ShowNotice`. The notice is a text block on the radial window, on the same fill and stroke, and the window widens so the line is on screen. A click on the notice clears it. Not exercised against a down Host in a live window. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Build succeeded. 0 warnings, 0 errors. Output below. No file lock. |
| Report lists commands and results | Pass | This file. |

## What changed

- Radial tab fill is `Color.FromArgb(168, 206, 228, 255)`. Stroke is `#4BA3FF`. Both live on `RadialTabChrome`. The “CE” mark is dark `(20, 32, 51)`.
- The VM chrome window is a tab centered on the top edge of the work area (`120×36` dip) when closed. Click, or a downward drag of at least 16px, opens it. Open, the header stays on that edge and the still hangs directly underneath. Together they are the quarter of the work area, centered on the top edge.
- The still window stays opaque and unfrosted. The picture well is the still fill, not the radial fill. `OfferGuest` still drops the HWND.
- Slot hits include the caption. Create agent, Contacts, and favorites share that hit. Create agent from the radial opens the wizard on the radial, in front of it. A Host miss is shown on the radial.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~Prop167ChromeTests"
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~ViewportDrawerTests"
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

Time Elapsed 00:00:05.20
```

### Caption hit, radial fill, top-edge tab

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Core -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll
  House.ChatDesktop.Tests -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
Test run for C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 7 ms - House.ChatDesktop.Tests.dll (net8.0)
```

### Existing viewport suite

```text
Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 103 ms - House.ChatDesktop.Tests.dll (net8.0)
```
