---
type: task
prop_id: PROP-5.4
prop_root: PROP-5-host-sqlite-concurrency-ownership
from: PM-01
to: QA-01
priority: P0
status: Pending
created: 2026-09-05
wave: wipeout-now
title: Concurrent soak — chat write + memory/SMS + dual tick + charter read
depends_on: PROP-5.1, PROP-5.2, PROP-5.3
proposal: docs/agents/unexecuted_proposals/host-sqlite-concurrency-ownership.md
report: docs/agents/reports/PROP-5.4-QA01-to-PM01.md
---

# PROP-5.4 — Concurrent soak gate

## Scope

Prove PROP-5 under overlap:

1. Chat/memory write storm + charter read
2. Overlapping SoulLoop ticks (timer + forced tick if available)
3. Optional SMS/memory path if harness exists
4. Host still boots; `/health` stays up while DB busy

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Soak script/log: no Sqlite concurrency exceptions |
| 2 | Single-flight tick behavior observed |
| 3 | Health remains available |
| 4 | Fail with repro if flake; no soft Pass |

## Evidence

Command lines, log excerpts, Pass/Fail table in report.
