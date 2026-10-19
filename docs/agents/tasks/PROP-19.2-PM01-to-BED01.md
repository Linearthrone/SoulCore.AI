---
type: task
prop_id: PROP-19.2
prop_root: PROP-19-her-screen-operator-clicks
from: PM-01
to: BED-01
priority: P0
status: Queued
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Park/COM Absolute delivery honesty for Kurt intervenes
depends_on: PROP-19.1 (soft — use matrix; may start after skim)
blocks: PROP-19.5 (hard — Kurt 3/3 needs delivery)
proposal: docs/agents/unexecuted_proposals/her-screen-operator-clicks.md
intake: docs/agents/tasks/PROP-19-TT01-to-PM01.md
accept: docs/agents/reports/PROP-19-PM01-accept.md
urgency: Kurt intervene path must land Absolute with park=ok visible
environment: TINA-main
---

# PROP-19.2 — Park/COM delivery honesty

**Activate:** `@Agents/BED-01.md` then `Start PROP-19.2.` after 19.1 skim (or in parallel if matrix already points at park/COM).

## Problem

Kurt’s overlay is alive (Q1) but Absolute presses do not stick in the guest. Mouse Integration clears Absolute when the host cursor sits on the embed; park must leave the **embed HWND** (not GA_ROOT — #171). Failures must not look like success.

## Solution

1. Confirm / harden park excludes = embed HWND only; log `park=ok|failed` on every Kurt button inject.
2. Fail loud on COM Absolute errors (startup log + surface Kurt can see if FED banner not ready).
3. Overlay lifecycle only where 19.1 proves attach loss — do not rebuild attach as the default fix.
4. Keep inject-on-release / capture-lost synthesis behavior; do not reintroduce Absolute-on-button-down under SetCapture.
5. Tests for park exclude policy / delivery log lines where unit-testable.

## Do not

- OS `SetCursorPos` as product click path
- `WS_EX_TRANSPARENT` on full-pane input child
- Soft-cursor paint rewrite (19.3 / 19.4)
- Mark wave Pass because #171 merged

## Acceptance

- [ ] Kurt center click lands on guest control with Presence ≥0.1.45+ this ticket (or clear residual bug named)
- [ ] Every inject attempt logs park + sent/COM outcome
- [ ] No silent “success” when park or COM fails
- [ ] Report: `docs/agents/reports/PROP-19.2-BED01-to-PM01.md`

## Handoff

Parallel with **19.3** OK if map and delivery are disjoint. Both needed before **19.5**.
