---
type: task
prop_id: PROP-20.15
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Done
created: 2026-11-17
title: Chat mind block loads Constitution floors from four-book charter
depends_on: PROP-20.14
floor_map: docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md
---

# PROP-20.15 — Constitution floors from books

**Activate:** `@Agents/BED-01.md`

## Problem

After 20.14, floors must come from **Constitution** book rows (title + floor line), not the old kind pile alone. Chat still receives Constitution floors; Personality/Attributes/Compass are stored but Compass-in-prompt rule remains Open (do not dump Compass into preamble yet unless already matching resting/floor law).

## Solution

1. `VictoriaMindBlock` / `ChatContextBuilder` / charter list APIs use book=Constitution for floors.
2. Identity/settings may expose book on anchors if cheap.
3. Tests: every Constitution title from the floor map appears in floors; non-Constitution titles are not required in the floor block.
4. Soft-start OK if 20.14 not merged — implement against the decision map + fail closed if book column missing.

## Acceptance

- [ ] Desk chat preamble Constitution floors match the five Constitution titles
- [ ] No weight on Constitution that can erase a line
- [ ] Report: `docs/agents/reports/PROP-20.15-BED01-to-PM01.md`
