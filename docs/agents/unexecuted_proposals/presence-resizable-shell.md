---
type: proposal
status: ticketed
tt_id: TT-01
created: 2026-09-16
updated: 2026-09-16
title: "[TINA-main] Presence shell — resize window + adjustable inner panes"
need: Operator must resize the whole Presence window and drag-adjust chat / Her browser / What she saw panes without a half-finished frameless shell
sent_at: 2026-09-16
prop_id: PROP-12
pm_intake: docs/agents/tasks/PROP-12-TT01-to-PM01.md
environment: TINA-main
---

# Presence shell — resizable window + panes

## 1. Need / Want

Presence is a **frameless** Avalonia shell (`SystemDecorations=None`). Today Kurt can **move** it (title drag) and **minimize/close**, but:

- **No OS resize grips** — cannot grow/shrink the whole window
- **No maximize/restore** chrome
- Inner layout is **fixed**: chat `*` + right column `260px`; sight panel hard-coded `Height="200"`
- Watching Playwright + chat needs **adjustable** chat vs browser vs sight

He wants this **through channels** (proposal → ticket → FED ship) so it is complete, not a half-made drag hack.

## 2. Goal & Success Criteria

1. **Whole window** resizable from edges/corners (frameless-safe via `BeginResizeDrag`)
2. **Maximize / restore** button + double-click title strip
3. **GridSplitter** between chat and right column (browser + sight)
4. **GridSplitter** between Her browser and What she saw
5. **Min sizes** so panes cannot collapse into unusable slivers
6. **Persist** window bounds + pane sizes in `LocalUiSettings` (`%LocalAppData%\HouseVictoria\ui-settings.json`)
7. Does **not** redesign materials, House drawer, or Playwright — layout chrome only

## 3. Context & Constraints

Verified on `main` (`House/House.ChatDesktop/MainWindow.axaml`):

| Bit | Today |
| --- | --- |
| Chrome | Frameless; drag strip; min/close only |
| Presence grid | `ColumnDefinitions="*,260"` — no splitter |
| Sight | `Height="200"` fixed |
| Prefs | `LocalUiSettings` has display name + notifications only |

Constraints: Avalonia 11.3.x; match existing metal/glass styles; no Host API changes; Settings view must still fill the client area.

## 4. Avenues

### A — Re-enable `SystemDecorations=Full` (rejected)

Would restore OS chrome and break the metal bevel shell Kurt already has.

### B — Edge grips + GridSplitters + persist (recommended)

Keep frameless shell. Add invisible edge/corner hit targets calling `BeginResizeDrag`. Add maximize. Use Avalonia `GridSplitter` for panes. Persist via existing JSON prefs.

### C — Only pop-out panels (rejected as sole fix)

Pop-out already exists; does not fix main window or docked proportions.

## 5. Recommended route

**Avenue B.** Ticket **PROP-12.1 → FED-01**. No BED/OPS unless prefs path becomes a problem (it will not).

## 6. Risks

| Risk | Mitigation |
| --- | --- |
| Grips steal clicks from title buttons | Grips only on outer 6px rim; title buttons keep z-order |
| Maximized + grips | Ignore resize when `WindowState == Maximized` |
| Splitter fights `*` columns | Side column absolute width; chat `*`; sight row absolute height |
| Halfway ship | Single FED ticket with ACs; report with build evidence |

## 7. Suggested PM handoff

| Split | Role | One-line |
| --- | --- | --- |
| PROP-12.1 | FED-01 | Frameless resize + maximize + pane splitters + persist |
