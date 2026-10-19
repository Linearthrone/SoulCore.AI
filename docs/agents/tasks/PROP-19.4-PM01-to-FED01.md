---
type: task
prop_id: PROP-19.4
prop_root: PROP-19-her-screen-operator-clicks
from: PM-01
to: FED-01
priority: P1
status: Pass
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Honest Her screen banner + soft-cursor paint on shared map
depends_on: PROP-19.3 Pass (hard — shared map); PROP-19.2 soft for inject-fail signals
blocks: none
proposal: docs/agents/unexecuted_proposals/her-screen-operator-clicks.md
intake: docs/agents/tasks/PROP-19-TT01-to-PM01.md
accept: docs/agents/reports/PROP-19-PM01-accept.md
urgency: no silent deaf pane; soft-cursor must not lie about the hit
environment: TINA-main
---

# PROP-19.4 — Presence honesty for inject / cursor

**Activate:** `@Agents/FED-01.md` then `Start PROP-19.4.` after 19.3 map contract exists.

## Scope

### In

1. Soft-cursor (and hover badge if applicable) painted from the **same** slot→FB map as Absolute inject.
2. Honest banner / status when inject fails, park fails, or input overlay is missing — no silent deaf Her screen.
3. Wire to Host/Presence signals from 19.2 where available.

### Out

- New teleop / OS cursor product path
- Redesigning Her screen chrome beyond honesty + cursor alignment
- Wheel ISSUE-20261014-001 full fix (coordinate only)

## Acceptance

- [ ] Soft-cursor tip matches inject point under shared map
- [ ] Operator-visible failure when Absolute/park/overlay fails
- [ ] Report: `docs/agents/reports/PROP-19.4-FED01-to-PM01.md`
