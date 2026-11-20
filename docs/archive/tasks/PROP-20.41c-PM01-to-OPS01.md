---
type: task
prop_id: PROP-20.41c
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-18
title: Restore Presence after aborted ALLSTART
depends_on: PROP-20.41b
---

# PROP-20.41c — Restore Presence (Release)

**Activate:** `@Agents/OPS-01.md`

Host `:7710` is healthy (`bondCounters`/`soak` present). ALLSTART aborted while starting ChatDesktop — **House.ChatDesktop is not running**.

Do not ForceRebuild Host unless health fails. Start Presence Release against `127.0.0.1:7710`. Confirm process up.

Report: `docs/agents/reports/PROP-20.41c-OPS01-to-PM01.md`
