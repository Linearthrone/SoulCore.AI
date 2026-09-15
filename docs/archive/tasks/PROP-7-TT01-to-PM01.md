---
type: proposal-intake
prop_id: PROP-7
prop_full: PROP-7-hermes-dead-surface-cleanup
from: TT-01
to: PM-01
priority: P1
status: Closed — shipped report-only (no PROP-7.1 ticket; see gated-hold exception), Pass; code on main
archived: 2026-09-15
report: docs/agents/reports/PROP-7.1-BED01-to-PM01.md
exception: docs/archive/tasks/PROP-7-11-PM01-gated-hold.md
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Hermes dead-surface cleanup"
proposal: docs/archive/proposals/hermes-dead-surface-cleanup.md
cluster_map: docs/archive/proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
blocked_by: PROP-5
---

# PROP-7 : [TINA-main] Hermes dead-surface cleanup

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/archive/proposals/hermes-dead-surface-cleanup.md`  
**Gate:** after **PROP-5 Pass** (sole Host lane).

## One-paragraph recommended route

Remove live `IHermesClient` / `HermesOptions` / `BackendHermes` remaps from Host DI and chat ctor; delete PreferHermes arms / `HermesToolRouting`; sync PRODUCT_ROOT + handbook claims. Delete-only on handler — **not** the PROP-8 structural split.

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-7.1 | BED-01 | Remove Hermes DI/config/handler surface + tests |
| PROP-7.2 | DOC/BED | PRODUCT_ROOT + handbook Hermes honesty |

TT-01 does not ticket BED.
