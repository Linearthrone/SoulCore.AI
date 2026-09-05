---
type: proposal-intake
prop_id: PROP-6
prop_full: PROP-6-desktop-drag-async-delay
from: TT-01
to: PM-01
priority: P1
status: Intake — TINA-main ticketing
created: 2026-09-05
sent_at: 2026-09-05
environment: TINA-main
mode: idea
title: "[TINA-main] Desktop drag — Thread.Sleep → async delay"
proposal: docs/agents/unexecuted_proposals/desktop-drag-async-delay.md
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
program: PROP-5..11 architecture-eval wipeout (sent together)
assignee_role: PM-01 (TINA)
---

# PROP-6 : [TINA-main] Desktop drag — async delay

**For:** **TINA-main** PM-01. **From:** TT-01. **Mode:** `idea`.  
**Proposal:** `docs/agents/unexecuted_proposals/desktop-drag-async-delay.md`

## One-paragraph recommended route

In `NativeDesktopControlBackend`, replace drag `Thread.Sleep` with cancellable `Task.Delay`. No Host/Memory/Hermes edits. Safe to ticket **in parallel with PROP-5**.

## Suggested next tickets (not binding)

| Split | Role | One-line |
| --- | --- | --- |
| PROP-6.1 | BED-01 | Sleep → Delay + desktop tool test |

TT-01 does not ticket BED.
