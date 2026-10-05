---
type: task
prop_id: PROP-16.1
prop_root: PROP-16-presence-host-update-and-stack-restart
from: PM-01
to: FED-01
priority: P1
status: Pass (code; Windows smoke pending)
created: 2026-10-05
updated: 2026-10-05
wave: presence-ops
title: LocalStackControl git/Host update APIs + Presence Update/Restart UI
depends_on: PROP-16.2 (script path; land together OK)
proposal: docs/agents/unexecuted_proposals/presence-host-update-and-stack-restart.md
intake: docs/agents/tasks/PROP-16-TT01-to-PM01.md
spec: docs/superpowers/specs/2026-10-05-presence-host-update-and-stack-restart-design.md
report: docs/agents/reports/PROP-16.1-FED01-to-PM01.md
---

# PROP-16.1 — Update pipeline + Restart stack UI

## Solution

1. Extend `LocalStackControl` with `GetGitStatusAsync`, `PullAsync`, `UpdateHostAsync`, `RestartStackAsync`.
2. Chrome: **Update** runs full pipeline; add **Restart stack** (confirm → detached script).
3. Settings → Updates: git/Host/Presence status; Check refreshes only; Update now / Restart stack match chrome.
4. Busy flags so Update and Restart cannot overlap. Dirty/non-FF never force-reset.

## Acceptance

- [ ] Chrome **Update** = pull-if-behind → Host rebuild/restart → Presence Velopack when available
- [ ] Chrome **Restart stack** detaches ALLSTOP→ALLSTART script; fails in UI if spawn/script missing
- [ ] Settings Check refreshes status only; Update now matches chrome
- [ ] Unit tests for git parse helpers + RestartStack missing-script path
- [ ] Report filed
