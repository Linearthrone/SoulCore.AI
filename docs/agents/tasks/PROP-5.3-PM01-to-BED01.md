---
type: task
prop_id: PROP-5.3
prop_root: PROP-5-host-sqlite-concurrency-ownership
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-09-05
wave: wipeout-now
title: Charter one R/W policy on memory DB path — end dual-open lie
depends_on: PROP-5.2
proposal: docs/agents/unexecuted_proposals/host-sqlite-concurrency-ownership.md
intake: docs/agents/tasks/PROP-5-TT01-to-PM01.md
report: docs/agents/reports/PROP-5.3-BED01-to-PM01.md
---

# PROP-5.3 — Charter ownership honesty

## Problem

`CharterService` xmldoc claims independent DB / no Host DI, but Host does `new CharterService(memoryOptions.ResolveDbPath())` — second opener on the same file as `SqliteMemoryStore`.

## Solution

1. One ownership model: Memory owns DDL/schema; charter reads/writes go through the **same path-keyed gate** (or shared session helper) — no second long-lived connection without the gate.
2. Fix xmldoc + DI comments to match reality.
3. Prefer minimal change (Avenue C light): keep one file; stop dual-open races.

## Do not

- Split charter to a second DB file
- Rewrite repository layout (PROP-11)
- Edit ChatWebSocketHandler structure beyond deleting nothing Hermes-related here

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | No ungated dual long-lived openers on memory DB path |
| 2 | xmldoc/DI comments match Program wiring |
| 3 | Charter seed/read tests still pass |
| 4 | Report lists before/after ownership model in one paragraph |
