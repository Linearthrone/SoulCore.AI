---
type: intake
prop_id: PROP-16-presence-host-update-and-stack-restart
from: TT-01
to: PM-01
priority: P1
status: Accepted
created: 2026-10-05
updated: 2026-10-05
title: Presence + Host update and full stack restart
proposal: docs/agents/unexecuted_proposals/presence-host-update-and-stack-restart.md
spec: docs/superpowers/specs/2026-10-05-presence-host-update-and-stack-restart-design.md
---

# PROP-16 intake — Presence + Host update / Restart stack

## Problem

Operator on Presence 0.1.7+ can hit **Update** and still run a stale Host (Playwright-only `/browser/view`) because Update never rebuilds Host. Full stack bounce still needs PowerShell.

## Ask

Ticket and ship the approved design:

`docs/superpowers/specs/2026-10-05-presence-host-update-and-stack-restart-design.md`

## Suggested handoff

- **PROP-16.1** FED — LocalStackControl + UI
- **PROP-16.2** OPS — `restart-stack.ps1`
