---
type: task
prop_id: PROP-19.1
prop_root: PROP-19-her-screen-operator-clicks
from: PM-01
to: QA-01
co_owners:
  - BED-01
priority: P0
status: Queued
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Home PC repro matrix — overlay alive, Absolute args, park, slot vs FB
depends_on: none
blocks: PROP-19.2 (soft — findings steer delivery); PROP-19.3 (soft — findings steer map)
proposal: docs/agents/unexecuted_proposals/her-screen-operator-clicks.md
intake: docs/agents/tasks/PROP-19-TT01-to-PM01.md
accept: docs/agents/reports/PROP-19-PM01-accept.md
urgency: desk intervene blocked until matrix shows where Absolute dies
environment: TINA-main
---

# PROP-19.1 — Home PC diagnose matrix

**Activate:** `@Agents/QA-01.md` then `Start PROP-19.1.` (BED-01 pairs on Host/Presence logging).

## Operator urgency

Kurt cannot click into the guest while the hover badge still tracks. Capture one honest session before more fixes land blind.

## Locked context

- Q1: badge / soft-cursor sibling **tracks** while clicks miss → overlay receives move.
- Q2: Windows **default** scale; Presence + Her screen on **one** monitor (multi-mon hardware OK).
- Presence **≥0.1.45** expected for park GA_ROOT fix (#171). Note build if older.

## Matrix (same session — Kurt + Victoria)

For each Kurt click attempt and one Victoria tool click, record:

1. Presence version / InformationalVersion
2. Does badge track over Her screen? (expect yes)
3. Does guest UI react to Kurt click? Y/N
4. `presence-startup.log` (or equivalent): `park=ok|failed`, Absolute coords, COM errors
5. Slot size vs guest FB (`IDisplay` / hub) — equal or lagging?
6. Soft-cursor tip vs Victoria Absolute hit (visual or logged) — rough px miss

Optional if easy: after Recover / re-embed, do Kurt clicks work briefly then die? (open Q4)

## Do not

- Ship a new inject path in this ticket
- OS-mouse teleop
- Close wheel ISSUE-20261014-001 here (note only if same session)

## Acceptance

- [ ] Written matrix with Kurt miss + Victoria offset from one Home PC session
- [ ] Presence version recorded; park line quoted if present
- [ ] Clear steer: delivery (19.2) vs map (19.3) vs both
- [ ] Report: `docs/agents/reports/PROP-19.1-QA01-to-PM01.md` (BED co-sign OK)

## Handoff

Findings unblock **19.2** / **19.3**. PM may parallelize both if matrix says both.
