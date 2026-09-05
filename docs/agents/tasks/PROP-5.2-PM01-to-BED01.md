---
type: task
prop_id: PROP-5.2
prop_root: PROP-5-host-sqlite-concurrency-ownership
from: PM-01
to: BED-01
priority: P0
status: Completed
created: 2026-09-05
wave: wipeout-now
title: SqliteMemoryStore async command serialization + busy_timeout
depends_on: PROP-5.1 preferred first (same PR OK)
proposal: docs/agents/unexecuted_proposals/host-sqlite-concurrency-ownership.md
intake: docs/agents/tasks/PROP-5-TT01-to-PM01.md
report: docs/agents/reports/PROP-5.2-BED01-to-PM01.md
---

# PROP-5.2 — Serialize SqliteMemoryStore DB work

## Problem

`SqliteMemoryStore` holds one long-lived `SqliteConnection`; `_gate` only covers dispose. Chat / SMS / tools / loop race the same connection.

## Solution (Avenue A — TT accepted)

1. `SemaphoreSlim(1,1)` (or equivalent) around **all** DB command paths — acquire for short critical sections only.
2. **Never** hold the gate across embedding / network / LLM I/O.
3. Set `busy_timeout` (PRAGMA or connection string) on open.
4. Preserve public interfaces; one LocalAppData SQLite file; no EF / second DB.

## Do not

- Hitchhike ChatWebSocketHandler split, Hermes purge, DI modules, vector index
- Connection-per-op factory rewrite (Avenue B) unless soak proves serialize latency unacceptable — file follow-up, do not expand this ticket silently

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | All store command paths go through the gate |
| 2 | Gate not held across network/embed |
| 3 | busy_timeout set on open |
| 4 | Existing memory/emotion/task tests still pass |

## Next

PROP-5.3 charter ownership → PROP-5.4 QA soak.
