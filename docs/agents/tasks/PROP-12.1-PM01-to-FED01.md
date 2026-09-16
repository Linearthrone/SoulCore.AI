---
type: task
prop_id: PROP-12.1
prop_root: PROP-12-presence-resizable-shell
from: PM-01
to: FED-01
priority: P1
status: Reported (Partial)
created: 2026-09-16
updated: 2026-09-16
wave: presence-layout
title: Presence frameless resize + inner pane splitters + persist
depends_on: none
proposal: docs/agents/unexecuted_proposals/presence-resizable-shell.md
intake: docs/agents/tasks/PROP-12-TT01-to-PM01.md
---

# PROP-12.1 — Resize + panes

## Solution

1. Frameless **edge/corner resize** (`BeginResizeDrag`) with 6px hit targets; no-op when maximized.
2. **Maximize / restore** window-chrome button; **double-click** title drag strip toggles maximize.
3. Presence layout: `GridSplitter` chat | right column; `GridSplitter` Her browser | What she saw.
4. Remove fixed sight `Height="200"`; use splitter-driven row height with **MinHeight**.
5. Persist window X/Y/W/H/maximized + side column width + sight row height in `LocalUiSettings`; restore on load; save on close / debounced size change.
6. Keep Settings view usable; do not break House drawer / pop-out.

## Do not

- Re-enable full OS decorations
- Redesign materials / House drawer (PROP-4)
- Change Host APIs

## Acceptance

- [x] Drag window edges/corners to resize when restored *(code; Windows QA pending)*
- [x] Maximize ↔ restore via button and title double-click *(code; Windows QA pending)*
- [x] Drag splitters to change chat vs browser vs sight sizes *(code; Windows QA pending)*
- [x] Restart Presence: sizes restore from `ui-settings.json` *(persist path unit-tested; Windows QA pending)*
- [x] `dotnet build` ChatDesktop succeeds; unit tests for settings round-trip

## Report

`docs/agents/reports/PROP-12.1-FED01-to-PM01.md`
