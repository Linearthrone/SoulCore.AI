---
type: task
prop_id: PROP-19.3
prop_root: PROP-19-her-screen-operator-clicks
from: PM-01
to: BED-01
priority: P0
status: In progress
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Unify slot→guest FB map for Kurt inject, Victoria Absolute, soft-cursor tip
depends_on: PROP-19.1 (soft)
blocks: PROP-19.4 (hard — FED paints shared map); PROP-19.5 Victoria grid (hard)
proposal: docs/agents/unexecuted_proposals/her-screen-operator-clicks.md
intake: docs/agents/tasks/PROP-19-TT01-to-PM01.md
accept: docs/agents/reports/PROP-19-PM01-accept.md
urgency: Victoria limp-aim and Kurt near-miss share one transform
environment: TINA-main
---

# PROP-19.3 — Shared coord contract

**Activate:** `@Agents/BED-01.md` then `Start PROP-19.3.`

## Problem

Soft-cursor is Presence paint; the hit is Absolute. Letterbox screenshot vs stretch-fill inject, Absolute `[+1,+1]`, and Auto-resize slot≠FB make Victoria land off the shown tip (and can make Kurt feel “dead” when Absolute misses UI).

## Solution

1. One **slot → guest FB** contract used by: Kurt `PostFromSurface`, Victoria Absolute, soft-cursor tip placement.
2. Prefer live `IDisplay` / hub FB; never treat embed slot size as FB when they disagree.
3. Document Absolute origin / rounding once; soft-cursor tip aligns to the same inject point.
4. Regression tests: center click when slot≠FB (existing stretch-fill case); edge samples if cheap.

## Do not

- Separate “vision map” that disagrees with inject
- Assume 1280×800 forever
- OS-mouse teleop

## Acceptance

- [ ] Single documented map API / helpers shared by inject + soft-cursor consumers
- [ ] Slot≠FB center regression green
- [ ] Victoria aim offset explained or fixed within ~8–12 px at edges in lab notes (final gate is 19.5)
- [ ] Report: `docs/agents/reports/PROP-19.3-BED01-to-PM01.md`

## Handoff

Unblocks **19.4** paint/banner. **19.5** owns contested-focus + corner grid Pass.
