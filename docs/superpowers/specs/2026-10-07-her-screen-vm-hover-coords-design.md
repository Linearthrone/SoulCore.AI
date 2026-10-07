# Her screen VM: hover coords (B) + soft cursor + live display

**Date:** 2026-10-07  
**Status:** Approved (Approach 1, coords = B hover only)  
**Depends:** PROP-16 embed HWND cache (PR #131)

## Goals

1. Live VM display in Her screen (HWND embed).
2. Operator hover coordinates on the VM surface for guidance (option B).
3. Her pink/teal soft cursor visible and aligned.
4. Bonus: clicks pass through to the embedded VirtualBox HWND (takeover).

## Non-goals

- Agent-cursor coordinate badge (option A/C)
- Driving guest clicks from Avalonia (native HWND receives clicks)
- Removing VirtualBox window chrome

## Design

- Soft cursor: stretch-to-fill mapping when embed is live (matches `MoveWindow`).
- Hover coords: poll system cursor vs Her-screen surface bounds (Win32 `GetCursorPos`) so Avalonia does not steal HWND input; map with stretch-to-fill to guest framebuffer pixels; show `click (x, y)` badge.
- JPEG path unchanged (Uniform letterbox map + Avalonia PointerMoved).
- Frame size from `/browser/view` hub; if missing while embed live, use last known or a documented default until first `desktop_screenshot`.
