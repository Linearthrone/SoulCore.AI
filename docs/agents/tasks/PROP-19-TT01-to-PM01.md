---
type: intake
prop_id: PROP-19-her-screen-operator-clicks
from: TT-01
to: PM-01
mode: idea
environment: TINA-main
status: Accepted — PM ticketed PROP-19.1–19.5
priority: P1
created: 2026-10-19
updated: 2026-10-19
title: "[TINA-main] Her screen — Kurt clicks dead; Victoria aim offset"
assignee_role: PM-01 (TINA)
proposal: docs/agents/unexecuted_proposals/her-screen-operator-clicks.md
pm_accept: docs/agents/reports/PROP-19-PM01-accept.md
pm_tickets:
  - docs/agents/tasks/PROP-19.1-PM01-to-QA01.md
  - docs/agents/tasks/PROP-19.2-PM01-to-BED01.md
  - docs/agents/tasks/PROP-19.3-PM01-to-BED01.md
  - docs/agents/tasks/PROP-19.4-PM01-to-FED01.md
  - docs/agents/tasks/PROP-19.5-PM01-to-QA01.md
---

# PROP-19 : [TINA-main] Her screen operator clicks + Victoria aim

**For:** **TINA-main** PM-01 (House Victoria / TINA).  
**From:** TT-01.  
**Mode:** `idea`.

## Proposal

[`docs/agents/unexecuted_proposals/her-screen-operator-clicks.md`](../unexecuted_proposals/her-screen-operator-clicks.md)

## Summary

Kurt cannot intervene on Her screen: his clicks on the embedded `victoria-sandbox` pane do not reach the guest. Victoria’s tool Absolute clicks do land but miss where the soft-cursor aims. Same COM Absolute stack; two failure modes. Soft-cursor is Presence paint, not the hit.

## Decisions already locked (Kurt 2026-10-19)

| Lock | Decision |
| --- | --- |
| Q1 Overlay feel | Hover badge / soft-cursor sibling **still tracks** while clicks miss → overlay alive; deprioritize “not attached” |
| Q2 DPI / monitors | Windows **default** display scale; multi-monitor hardware, but Presence + Her screen stay on **one** monitor |
| Q3 Path | TT default: **VM embed** / Ubuntu in `victoria-sandbox` (COM Absolute) |
| Q4 Recover life/die | Open — fold into 19.1 matrix |
| Product path | **No** OS `SetCursorPos` teleop (PROP-14 soft-cursor lock) |

## Related in-flight

Presence **0.1.45** / PR #171 fixed GA_ROOT park exclude (`park=failed` on maximized Presence). Treat as Avenue C progress; **do not** Pass PROP-19 on that alone — Victoria aim + delivery honesty remain.

## Suggested splits

PM may re-split / reassign.

- `PROP-19.1` QA-01 + BED-01 — Home PC matrix: button-down seen? Absolute args? `park=` line? slot vs `IDisplay` FB? Kurt + Victoria same session; confirm Presence ≥0.1.45.
- `PROP-19.2` BED-01 — Park/COM delivery honesty + remaining Kurt miss after #171; overlay lifecycle only if 19.1 proves loss.
- `PROP-19.3` BED-01 — Unify slot→FB map for Kurt inject, Victoria Absolute, soft-cursor tip; regressions for slot≠FB.
- `PROP-19.4` FED-01 — Honest banner when inject/overlay fails; soft-cursor paint on shared map.
- `PROP-19.5` QA-01 — Contested-focus Kurt 3/3; Victoria corner grid ≤~8–12 px; coordinate wheel ISSUE-20261014-001 (don’t swallow).

## Kill criteria

- Kurt still deaf after contested-focus smoke with live overlay + park=ok logged.
- Victoria edge error stays >~12 px after shared map.
- OS-mouse drive shipped as product path.
- `WS_EX_TRANSPARENT` reintroduced on full-pane input child.

## What to decide first

Ticket **19.1** immediately. Prefer **19.2** (delivery) before or tightly with **19.3** (map). Do not mark wave Pass on park-only.

## PM disposition (2026-10-19)

**Accepted.** Avenue C then B. Tickets issued — see `docs/agents/reports/PROP-19-PM01-accept.md`.

Start with `@Agents/QA-01.md` / `@Agents/BED-01.md` → `Start PROP-19.1.`
