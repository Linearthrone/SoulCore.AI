---
type: task
prop_id: PROP-19.5
prop_root: PROP-19-her-screen-operator-clicks
from: PM-01
to: QA-01
priority: P0
status: Queued
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Contested-focus Kurt 3/3 + Victoria corner grid ≤ tolerance
depends_on: PROP-19.2 Pass; PROP-19.3 Pass; PROP-19.4 preferred
blocks: none
proposal: docs/agents/unexecuted_proposals/her-screen-operator-clicks.md
intake: docs/agents/tasks/PROP-19-TT01-to-PM01.md
accept: docs/agents/reports/PROP-19-PM01-accept.md
urgency: wave Pass gate — intervene + aim trust
environment: TINA-main
---

# PROP-19.5 — Her screen click QA smoke

**Activate:** `@Agents/QA-01.md` then `Start PROP-19.5.` after 19.2 + 19.3 Pass (19.4 preferred for banner/cursor).

## Smoke

1. **Kurt:** center click on a known guest control, focus contested (chat/chrome vs Her screen) — **3/3** land.
2. **Victoria:** corner grid Absolute vs soft-cursor tip — miss ≤ ~8–12 px at edges.
3. Presence version + `park=ok` samples attached.
4. Note wheel ISSUE-20261014-001 status; do not swallow into this Pass.

## Kill / fail

- Kurt still deaf with badge tracking and park=ok claimed
- Victoria edge miss stays >~12 px after shared map
- OS-mouse path shipped

## Acceptance

- [ ] Kurt 3/3 contested-focus evidence
- [ ] Victoria corner grid within tolerance
- [ ] Report: `docs/agents/reports/PROP-19.5-QA01-to-PM01.md`
