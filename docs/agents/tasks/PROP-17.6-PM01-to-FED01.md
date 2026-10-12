---
type: task
prop_id: PROP-17.6
prop_root: PROP-17-victoria-between-talks
from: PM-01
to: FED-01
priority: P0
status: Pending
created: 2026-10-12
updated: 2026-10-12
wave: between-talks
title: Host Stop stays enabled while Starting Host; clear busy on timeout
depends_on: PROP-17.1 Partial (scripts force-kill exist); PROP-17.4 shell (System Host buttons)
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
intake: docs/agents/tasks/PROP-17-TT01-to-PM01.md
closes_gap: PROP-17.1 Avalonia Stop UI
urgency: same-day — operator pull-the-plug path during stuck start
environment: TINA-main
---

# PROP-17.6 — Host Stop unstick (Presence UI)

**Activate:** `@Agents/FED-01.md` then `Start PROP-17.6.`

## Problem

PROP-17.1 landed `start-soulcore.ps1` timeout force-kill, but Presence still treats Host start as a busy state that disables Stop / leaves “Starting Host…” stuck. LinearThrone’s pull-the-plug path is the System Stop button during a hung start.

## Solution

1. While Host start is in progress, **Stop stays enabled** and calls the existing stop/force-kill path (`LocalStackControl` / `stop-soulcore.ps1`).
2. On start timeout or failure, **always clear** the “Starting Host…” busy flag / activity copy.
3. Do not block Stop on the same busy flag that disables Start/Restart overlap if that would strand the operator — prefer Stop always available during start.

## Do not

- Re-open three-tab shell redesign (17.4 done)
- Auto-start VM
- Drop PROP-16 System update/restart block

## Acceptance

- [ ] Stop clickable during “Starting Host…”
- [ ] Stuck/timeout start clears busy UI; Stop force-kills Host
- [ ] Report: `docs/agents/reports/PROP-17.6-FED01-to-PM01.md`
