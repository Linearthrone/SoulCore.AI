---
type: task
prop_id: PROP-18.1
prop_root: PROP-18-chronology-and-felt-persistence
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-19
title: Dated recall compose (today-first + relevance)
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
---

# PROP-18.1 — Dated memory compose

**Activate:** `@Agents/BED-01.md`  
**Law:** `prop-18-q-decisions.md` Q2; proposal chronology-and-felt-persistence.md

## Do

1. Chat/SMS memory path returns **dated** lines (`occurred_at` visible in `[Memory]`).
2. Compose **today-first**, then relevant older (PROP-20 Q2). Keep similarity search.
3. Model can order two stored episodes by day without inventing the clock.
4. Tests with real output. Do not invent undated theater.

## Out of scope

Activities log UI, idle agency budget, voice wake.

## Report

`docs/agents/reports/PROP-18.1-BED01-to-PM01.md`
