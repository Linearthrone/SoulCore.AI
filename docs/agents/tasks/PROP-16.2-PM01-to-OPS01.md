---
type: task
prop_id: PROP-16.2
prop_root: PROP-16-presence-host-update-and-stack-restart
from: PM-01
to: OPS-01
priority: P1
status: Pass (script; Windows smoke pending)
created: 2026-10-05
updated: 2026-10-05
wave: presence-ops
title: restart-stack.ps1 ALLSTOP then ALLSTART with log
depends_on: none
proposal: docs/agents/unexecuted_proposals/presence-host-update-and-stack-restart.md
intake: docs/agents/tasks/PROP-16-TT01-to-PM01.md
spec: docs/superpowers/specs/2026-10-05-presence-host-update-and-stack-restart-design.md
report: docs/agents/reports/PROP-16.2-OPS01-to-PM01.md
---

# PROP-16.2 — restart-stack.ps1

## Solution

Add `House/scripts/restart-stack.ps1` that:

1. Logs to `House/artifacts/restart-stack.log`
2. Runs repo-root `ALLSTOP.ps1`
3. Runs repo-root `ALLSTART.ps1` (default switches)
4. Exits non-zero if either step fails

Presence must spawn this **detached** (`wait: false`) so ALLSTOP can kill ChatDesktop.

## Acceptance

- [ ] Script exists and is invokable from repo root via `powershell -File House\scripts\restart-stack.ps1`
- [ ] Log path created under `House/artifacts/`
- [ ] Report filed
