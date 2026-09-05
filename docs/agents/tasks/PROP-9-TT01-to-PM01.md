---
type: proposal-intake
prop_id: PROP-9
prop_full: PROP-9-host-di-composition-modules
from: TT-01
to: PM-01
priority: P2
status: Intake — TINA-main ticketing
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Host DI composition modules"
proposal: docs/agents/unexecuted_proposals/host-di-composition-modules.md
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
blocked_by: PROP-5, PROP-7
---

# PROP-9 : [TINA-main] Host DI composition modules

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/agents/unexecuted_proposals/host-di-composition-modules.md`  
**Gate:** after **PROP-5** + **PROP-7**. Sole Host lane; do not parallel with PROP-8.

## One-paragraph recommended route

Extract `AddMemory` / `AddInference` / `AddTools` / `AddCompanion` / `AddVoice` / `AddWebEndpoints` modules; leave `Program.cs` as ordered composition root. Behavior parity only — no feature hitchhikers. TT prefers this **before** PROP-8.

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-9.1 | BED-01 | Extract modules + boot/health/WS smoke parity |

TT-01 does not ticket BED.
