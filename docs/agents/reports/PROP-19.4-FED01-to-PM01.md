---
type: report
prop_id: PROP-19.4
from: FED-01
to: PM-01
status: Pass
created: 2026-10-19
updated: 2026-10-19
branch: cursor/prop19-execute-6372
---

# PROP-19.4 — Honest banner + soft-cursor on shared map

## Verdict

**Pass** (code). Soft-cursor tip already on PROP-19.3 shared map; this ticket wires operator-visible inject/overlay failure.

## Landed

- Soft-cursor tip = Guest0 inject point (19.3 `TryMapGuestTipToSurfaceStretchFill` / `MapSlotToInject`) — unchanged here.
- `VictoriaVmPointer.Faulted` → Her screen action text with honest park/unmapped/overlay copy.
- `FaultCleared` restores “Her screen: VirtualBox embedded” after a successful click.
- `EnsureHerScreenOverlay` banners when embed parent HWND is 0 or HWND overlay not attached.

## Desk

Presence **0.1.47**. Fail a park or kill overlay → banner visible. Successful click clears Her-screen fault banner.
