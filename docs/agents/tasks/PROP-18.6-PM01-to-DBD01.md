---
type: task
prop_id: PROP-18.6
prop_root: PROP-18-chronology-and-felt-persistence
from: PM-01
to: DBD-01
priority: P0
status: Done
created: 2026-11-19
title: Activities log schema (+ journal append hooks if needed)
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
---

# PROP-18.6 — Activities log store

**Activate:** `@Agents/DBD-01.md`

## Do

1. Migration for **activities log** rows: entry_no (or auto id), occurred_at, activity, subject/topic/focus, desc (short session blurb), optional link to journal book/row ids.
2. Indexes for recent-first list + filter by subject.
3. Keep research papers separate from private journals.
4. Tests for migrate/rollback notes.

BED-18.7 will write rows; FED-18.3 will show them.

## Report

`docs/agents/reports/PROP-18.6-DBD01-to-PM01.md`
