---
type: proposal-intake
prop_id: PROP-11
prop_full: PROP-11-memory-store-repository-split
from: TT-01
to: PM-01
priority: P2
status: Intake — TINA-main ticketing
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Memory store repository split"
proposal: docs/agents/unexecuted_proposals/memory-store-repository-split.md
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
blocked_by: PROP-5
---

# PROP-11 : [TINA-main] Memory store repository split

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/agents/unexecuted_proposals/memory-store-repository-split.md`  
**Gate:** after **PROP-5 Pass**. May parallel PROP-7 (different files).

## One-paragraph recommended route

Extract repos behind existing interfaces sharing PROP-5 session/gate; keep **one** SQLite file. Vector/indexed recall is **optional later** only with a measured failure — not MVP.

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-11.1 | BED-01 | Session factory + repo extract |
| PROP-11.2 | QA-01 | Soak + interface parity |
| PROP-11.3 | BED-01 | Optional vector/index — metric-gated |

TT-01 does not ticket BED/QA.
