---
type: report
prop_id: PROP-19.3
prop_root: PROP-19-her-screen-operator-clicks
from: BED-01
to: PM-01
status: Pass
created: 2026-10-19
updated: 2026-10-19
presence_version: 0.1.46
---

# PROP-19.3 — Shared slot→guest FB map — Pass

## Map contract (PROP-19.3)

Documented on `VictoriaBrowserCoordMap` (Presence). One stretch-fill pipeline for HWND embed:

1. **ResolveGuestFramebuffer** — live `IDisplay` > hub/screenshot > HWND slot > 1280×800 default. Never treat slot as FB when display/hub disagree.
2. **TryMapPointerStretchFill** — slot client px → Guest0 (`Round` AwayFromZero, clamp).
3. **ToAbsolute** — Guest0 → VirtualBox Absolute `[+1,+1]` (SDK). Soft-cursor / agent tools stay on Guest0; Absolute is inject-only.
4. **TryMapGuestTipToSurfaceStretchFill** — inverse of (2): Guest0 → soft-cursor **tip** (no glyph centering).

Composed helper: **`MapSlotToInject`** returns Guest0 + Absolute together.

JPEG / Playwright stay on Uniform letterbox APIs — do not mix with embed Absolute.

Host Victoria Absolute (`VboxConsoleMouse.PlanAbsolutePress`) keeps identical `+1,+1` (documented lockstep; House cannot reference Inference).

## Done

1. Inventory: letterbox vs stretch-fill, Absolute `[+1,+1]`, `ResolveGuestFramebuffer`, soft-cursor tip vs inject.
2. Single documented map API on `VictoriaBrowserCoordMap`; Kurt `PostFromSurface` (`MapSurfaceToGuest`) uses `MapSlotToInject`; soft-cursor HWND tip uses `TryMapGuestTipToSurfaceStretchFill`.
3. Presence bumped **0.1.45 → 0.1.46**.

## Evidence

- Commit: `15254c615c4d564bb0ec90c2e54052f9cde8168e` on `cursor/prop19-execute-6372`
- `VictoriaBrowserCoordMapTests` + `VictoriaVmPointerPlanTests`: **61 passed**
  - Slot≠FB center (900×600 slot, 1280×800 FB) → Guest0 (640,400) / Abs (641,401); tip round-trips
  - Edge samples (corners) tip ↔ Guest0 ↔ Absolute
  - Hub preferred over slot when they disagree

## Files

- `House/House.ChatDesktop/Services/VictoriaBrowserCoordMap.cs` — contract + `MapSlotToInject` / tip inverse
- `House/House.ChatDesktop/Services/VictoriaVmPointer.cs` — inject uses shared map
- `House/House.ChatDesktop/Services/VictoriaVmPointerPlan.cs` — Absolute/FB docs → CoordMap
- `House/House.ChatDesktop/MainWindow.BrowserView.cs` — soft-cursor tip = inject Guest0
- `SoulCore/SoulCore.Inference/Tools/Desktop/VboxConsoleMouse.cs` — Absolute parity note
- Tests + Presence `0.1.46`

## Handoff — FED 19.4 still needs for banner

- Wire honest banner / status when Absolute, park, or `HerScreenInput` overlay fails (signals from **19.2**)
- Keep soft-cursor paint on this shared tip map (already wired for HWND embed)
- Do not invent a second vision/letterbox map for embed Absolute
- Report: `docs/agents/reports/PROP-19.4-FED01-to-PM01.md`

**19.5** owns contested-focus Kurt 3/3 + Victoria corner grid ≤ ~8–12 px.
