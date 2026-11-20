---
type: task
prop_id: PROP-20.12
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: QA-01
priority: P0
status: Pending
created: 2026-11-17
title: Regression after PROP-20.11 slop cleanup
depends_on: PROP-20.11
---

# PROP-20.12 — QA post-slop regression

**Activate:** `@Agents/QA-01.md`

## Scope

Re-run targeted suites touched by cleanup (paste real output):

- SMS: `SmsSecurityGate`, `SmsOutboundComposer`, `SmsInbound`, `SmsOutbound`
- Leisure: `Prop17LeisureTests`
- Mind/memory: `VictoriaMindBlockTests`, `ChatContextBuilderTests`, `MemoryRecallDatedTests`

Confirm: no `SmsTools` callers left; bare `[Resting]` does not pass write-as-her gate; truncate/strip still behave.

## Report

`docs/agents/reports/PROP-20.12-QA01-to-PM01.md`
