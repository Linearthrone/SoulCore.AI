---
type: proposal-intake
prop_id: PROP-5
prop_full: PROP-5-host-sqlite-concurrency-ownership
from: TT-01
to: PM-01
priority: P0
status: Intake — TINA-main ticketing
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Host SQLite concurrency + charter ownership + SoulLoop single-flight"
proposal: docs/agents/unexecuted_proposals/host-sqlite-concurrency-ownership.md
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
---

# PROP-5 : [TINA-main] Host SQLite concurrency + charter ownership + SoulLoop single-flight

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/agents/unexecuted_proposals/host-sqlite-concurrency-ownership.md`  
**Program map:** `docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md`

This is the **lead Host lane** of the architecture-eval wipeout (PROP-5..11 sent together). Keep ≤1 open PR editing `Program.cs` / `ChatWebSocketHandler`.

## One-paragraph recommended route

Serialize all `SqliteMemoryStore` DB work (`SemaphoreSlim` / equivalent) + `busy_timeout`; fix charter so Host does not dual-open the memory DB path; SoulLoop `TickAsync` single-flight (skip on overlap) + atomic tick counter. Prove with concurrent soak. Do **not** hitchhike handler split, Hermes purge, DI modules, or vector index.

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-5.1 | BED-01 | SoulLoop single-flight + atomic `_tickCount` |
| PROP-5.2 | BED-01 | SqliteMemoryStore async command serialization + busy_timeout (no lock across network) |
| PROP-5.3 | BED-01 | Charter one R/W policy on memory DB path; Memory owns DDL; fix xmldoc |
| PROP-5.4 | QA-01 | Concurrent soak: chat write + SMS/memory + dual tick + charter read |

## Parallel NOW (other intakes)

PROP-6 (desktop async) may ticket immediately beside this. PROP-1 / PROP-2 / PROP-4 continue. PROP-7..11 wait for gates in the cluster map.

TT-01 does not ticket BED/QA.
