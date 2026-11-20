---
type: task
prop_id: PROP-20.27c
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-18
title: Restore Presence after aborted ALLSTART overlap
depends_on: PROP-20.27b
---

# PROP-20.27c — Restore Presence (Release)

**Activate:** `@Agents/OPS-01.md`

Host `:7710` is healthy (`0.1.54`). An overlapping ALLSTART shell aborted; **House.ChatDesktop is not running**.

Do **not** ForceRebuild Host unless health fails. Start Presence Release against `127.0.0.1:7710` (ALLSTART Presence-only path or project start script). Confirm process + Host still ok.

Report: `docs/agents/reports/PROP-20.27c-OPS01-to-PM01.md`
