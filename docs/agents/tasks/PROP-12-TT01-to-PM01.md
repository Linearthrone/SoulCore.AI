---
type: proposal-intake
prop_id: PROP-12
from: TT-01
to: PM-01
priority: P1
status: Ticketed — PROP-12.1 FED
created: 2026-09-16
updated: 2026-09-16
pm_tickets: docs/agents/tasks/PROP-12.1-PM01-to-FED01.md
environment: TINA-main
mode: idea
title: "[TINA-main] Presence shell — resize window + adjustable inner panes"
proposal: docs/agents/unexecuted_proposals/presence-resizable-shell.md
assignee_role: PM-01 (TINA)
---

# PROP-12 : Presence shell — resize window + adjustable inner panes

**For:** TINA-main PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/agents/unexecuted_proposals/presence-resizable-shell.md`

## One-paragraph recommended route

Keep the frameless metal shell. Add edge/corner resize via Avalonia `BeginResizeDrag`, maximize/restore chrome + title double-click, `GridSplitter` between chat ↔ (browser/sight) and browser ↔ sight, min sizes, and persist bounds/pane sizes in `LocalUiSettings`. No Host changes. No material redesign.

## Suggested next tickets

| Role | One-line |
| --- | --- |
| FED-01 | PROP-12.1 — resize + maximize + splitters + persist |

TT-01 does not ticket FED directly beyond this intake; PM owns PROP-12.1.
