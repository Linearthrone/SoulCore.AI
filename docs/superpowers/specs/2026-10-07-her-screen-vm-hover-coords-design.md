# Her screen VM: coords badge (C) + soft cursor + live display

**Date:** 2026-10-07  
**Status:** Approved (Approach 1, coords = C both)  
**Depends:** PROP-16 embed HWND cache (PR #131); hover-only B (PR #133)

## Goals

1. Live VM display in Her screen (HWND embed).
2. Coords badge option **C**: her agent soft-cursor guest coords always; operator hover guest coords while hovering.
3. Her pink/teal soft cursor visible and aligned.
4. Bonus: clicks pass through to the embedded VirtualBox HWND (takeover).

## Non-goals

- Agent-only badge (option A)
- Hover-only badge (option B) — superseded by C
- Driving guest clicks from Avalonia (native HWND receives clicks)

## Design

- Soft cursor: stretch-to-fill mapping when embed is live (matches embed `SetWindowPos`).
- Badge text: `her (x, y)` from Host `cursorX`/`cursorY`; while operator pointer is over the surface append ` · you click (x, y)` (stretch-to-fill guest map).
- **Z-order:** SetParent’d VirtualBox HWND paints above Avalonia siblings. While embed is live, soft cursor + badge render in an owned click-through window (`WS_EX_TRANSPARENT` / `NOACTIVATE`) synced to the Her screen slot — not in-tree Canvas/Border.
- Hover coords: poll system cursor vs Her-screen surface bounds (Win32 `GetCursorPos`) so Avalonia does not steal HWND input.
- JPEG path: Uniform letterbox hover map + Avalonia PointerMoved; her line appears when soft cursor is active (vbox-guest).
- Frame size from `/browser/view` hub; if missing while embed live, use last known or default until first `desktop_screenshot`.
- Clipboard on surface click: prefer operator `click (x, y)`; else her position as `click (x, y)`.
- Embed resize: strip overlapped chrome styles on SetParent; `SyncSizeToSlot` on surface/splitter resize (DIP × DPI → `SetWindowPos`) so the VM fills the Her screen slot instead of clipping the right edge. Default side column 520 (was 260).
