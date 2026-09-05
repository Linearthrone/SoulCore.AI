---
type: proposal-intake
prop_id: PROP-10
prop_full: PROP-10-inference-clients-tools-split
from: TT-01
to: PM-01
priority: P2
status: Intake — TINA-main ticketing
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Inference clients vs tools split"
proposal: docs/agents/unexecuted_proposals/inference-clients-tools-split.md
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
blocked_by: PROP-7
---

# PROP-10 : [TINA-main] Inference clients vs tools split

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/agents/unexecuted_proposals/inference-clients-tools-split.md`  
**Gate:** after **PROP-7**. May parallel PROP-9 / PROP-11 if Host/Memory fences held.

## One-paragraph recommended route

Folder (then optional project) boundary: `Clients/` vs `Tooling/` vs `Tools/*`. No ChatWebSocketHandler move; no SQLite changes; avoid Program.cs churn (defer renames to PROP-9).

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-10.1 | BED-01 | Clients vs tooling folder boundary |
| PROP-10.2 | BED-01 | Optional SoulCore.Tools project extract |

TT-01 does not ticket BED.
