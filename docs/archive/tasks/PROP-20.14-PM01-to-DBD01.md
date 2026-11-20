---
type: task
prop_id: PROP-20.14
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: DBD-01
priority: P0
status: Done
created: 2026-11-17
title: Charter four-book schema (Constitution / Personality / Attributes / Compass)
depends_on: none
floor_map: docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md
blocks: PROP-20.15
---

# PROP-20.14 — Four-book schema

**Activate:** `@Agents/DBD-01.md`

## Problem

Live `charter_anchors` use kind=identity/safety/value/boundary. Step 4 needs each row in exactly one of Constitution, Personality, Attributes, Compass.

## Solution

1. Migration adding a durable book assignment (column or join table) with CHECK limited to the four book ids.
2. Seed/migrate existing rows per floor map decision file (title → book). Every live title gets exactly one book.
3. Constitution rows must remain loadable as floors (no weight column that can erase a Constitution line).
4. Tests: 18 titles mapped; unknown book rejected; rollback notes.

## Do not

- Rewrite prompt composer (BED 20.15)
- Invent new titles

## Acceptance

- [x] Schema + data: all 18 titles have exactly one book matching the decision file
- [x] Tests + report: `docs/agents/reports/PROP-20.14-DBD01-to-PM01.md`
