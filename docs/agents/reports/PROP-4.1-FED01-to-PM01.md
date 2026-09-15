---
type: report
prop_id: PROP-4.1
from: FED-01
to: PM-01
priority: P1
status: Partial
created: 2026-09-05
landed: 2026-09-14
branch: cursor/prop4-presence-drawer-8a1f
environment: Linux cloud agent (TINA-main tree)
mockup: docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png
---

# PROP-4.1 — House drawer + honest HUD (FED-01 → PM-01)

**Verdict: Partial** — cloud-feasible slice complete (structure, honesty contracts, build, unit tests). Full visual parity with mockup and live Host wiring require Kurt's Windows Presence box.

## What shipped (already on main, verified this branch)

| Requirement | Status | Evidence |
| --- | --- | --- |
| Bottom House drawer tab (closed = chat full-bleed) | Done | MainWindow.axaml HouseDrawerTab + ServicesPanel toggle |
| Open = lamp tray (SoulCore, Ollama, Unreal, Comfy, CUA, Sandbox) | Done | UniformGrid lamps |
| Closed-drawer red pip when SoulCore or Unreal down | Done | HouseDrawerPip + RefreshServicesPanelAsync |
| SoulCore hold ~1.5s to stop (click starts when down) | Done | LampSoulCore_PointerPressed / hold timer |
| Identity strip: name · mood gem · activity line | Done | Title strip |
| Mood from emotion.snapshot only (not loop.want) | Done | ApplyLoopWant skips HUD restamp |
| Activity from Host currentActivity (not want slogans) | Done | ApplyHonestActivity + SoulCoreHealthSnapshot.CurrentActivity |
| SoulLoop phrase-bank filtered from chat bubbles | Done | IsAutomatedProactiveLine |
| Sight: last still + datetime stamp + folder (scratch only) | Done | ScreenPanel + DesktopViewStampText + folder guard |
| Memory-sight dir never opened by Folder button | Done | DesktopViewOpenFolder_Click redirect |
| App icon in window chrome | Done | Assets/house-victoria.ico |
| Metal/glass material pass (no decorative screws) | Partial | App.axaml bevel brushes + brushed-charcoal.png |

## Cloud verification (this run)

```
cd House/House.ChatDesktop && dotnet restore && dotnet build
Build succeeded. 0 Warning(s) 0 Error(s)

cd House/House.ChatDesktop.Tests && dotnet test
Total tests: 16  Passed: 16
```

New in this branch: PresenceHonestyTests, InternalsVisibleTo, RadialGradientBrush RadiusX/Y fix.

## Partial — why not Pass

1. No GUI walkthrough — House.ChatDesktop is WinExe; Linux cloud cannot launch Presence against mockup.
2. Mockup asset missing from repo (presence-lamp-drawer-closed-open.png).
3. Host currentActivity BED slice — UI ready; end-to-end depends on BED-01.
4. Material depth needs on-machine Windows QA.

## Out of scope

- PROP-4.2 installer / Start menu / auto-update (OPS)
- SMS/UE lanes

## Recommended next steps

| Owner | Action |
| --- | --- |
| QA-01 | Windows smoke: drawer, pip, hold-stop SoulCore, folder opens scratch not memory-sight |
| BED-01 | Ensure /health presence.currentActivity is populated |
| OPS-01 | PROP-4.2 installer + update toast |

## Landing addendum (2026-09-14)

This report was written on `cursor/prop4-presence-drawer-8a1f` and never reached `main`, so it
was rescued **docs-only**. The verdict above is unchanged: still **Partial**, still gated on a
Windows Presence box.

Everything in the "What shipped (already on main)" table was verified against `main` and still
holds. The three items listed as *new in this branch* have **not** landed and remain only on
`cursor/prop4-presence-drawer-8a1f`:

- `House.ChatDesktop.Tests/PresenceHonestyTests.cs` — `main` has only `SoulCoreWsClientAuthTests`,
  so the "16 tests passed" figure above describes the source branch, not `main`.
- `House.ChatDesktop/Properties/AssemblyInfo.cs` (`InternalsVisibleTo`) — absent from `main`.
- The `RadialGradientBrush` `Radius` → `RadiusX`/`RadiusY` fix in `MainWindow.Presence.cs` —
  `main` still sets `Radius`.

Landing that slice is follow-up work and needs its own PR; keeping it out of the rescue PR avoids
mixing unreviewed Presence code into a docs recovery.
