---
type: proposal-intake
prop_id: PROP-8
prop_full: PROP-8-chat-orchestration-decomposition
from: TT-01
to: PM-01
priority: P1
status: Closed — shipped report-only (no PROP-8.1 ticket; see gated-hold exception), Pass; code on main
archived: 2026-09-15
report: docs/agents/reports/PROP-8.1-BED01-to-PM01.md
exception: docs/archive/tasks/PROP-7-11-PM01-gated-hold.md
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Chat orchestration decomposition"
proposal: docs/archive/proposals/chat-orchestration-decomposition.md
cluster_map: docs/archive/proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
blocked_by: PROP-5
prefer_after: PROP-9
---

# PROP-8 : [TINA-main] Chat orchestration decomposition

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/archive/proposals/chat-orchestration-decomposition.md`  
**Gate:** after **PROP-5**; **prefer after PROP-9**. Sole Host lane while open.

## One-paragraph recommended route

Strangler-extract `ChatWebSocketHandler`: session runner + command handlers; one `ChatContextBuilder`; history deque/ring; gated parallel context reads only after PROP-5 soak. Fold prompt/history/parallel-read Better-ifs here — do not mint three PROPs.

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-8.1 | BED-01 | History deque/ring |
| PROP-8.2 | BED-01 | ChatContextBuilder |
| PROP-8.3 | BED-01 | Strangler handlers / session runner |
| PROP-8.4 | BED-01 | Parallel context reads (PROP-5 gate) |
| PROP-8.5 | QA-01 | Chat + tool-loop regression soak |

TT-01 does not ticket BED/QA.
