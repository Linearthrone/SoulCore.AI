---
type: task
prop_id: PROP-5.1
prop_root: PROP-5-host-sqlite-concurrency-ownership
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-09-05
wave: wipeout-now
title: SoulLoop TickAsync single-flight + atomic tick counter
depends_on: none
proposal: docs/agents/unexecuted_proposals/host-sqlite-concurrency-ownership.md
intake: docs/agents/tasks/PROP-5-TT01-to-PM01.md
program: docs/agents/tasks/PROP-5-11-PM01-program-accept.md
report: docs/agents/reports/PROP-5.1-BED01-to-PM01.md
---

# PROP-5.1 — SoulLoop single-flight

## Problem

Hosted timer and WS `loop.tick` can overlap `TickAsync`; `_tickCount++` is racy. Duplicate side effects under load.

## Solution

1. Add process-wide single-flight gate on `TickAsync` (skip / busy-ack on overlap — **do not queue**).
2. Replace tick counter increment with `Interlocked` (or equivalent).
3. Keep SoulLoop fail-closed / skip-if-busy until PROP-5.2 lands if needed.
4. Unit/integration proof of overlapping ticks → only one runs.

## Do not

- Touch `SqliteMemoryStore` command paths (PROP-5.2)
- Touch charter dual-open (PROP-5.3)
- Split `ChatWebSocketHandler` structure (PROP-8)
- Hermes / DI module work (PROP-7 / 9)

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Overlapping `TickAsync` callers: at most one executes body |
| 2 | Tick counter not lost/racy under parallel invokes |
| 3 | Tests green; report cites files + evidence |

## Fence

May share one BED PR with 5.2/5.3 **only if** same author sequential commits; still **sole Host lane**.
