---
type: task
prop_id: PROP-18.12
from: PM-01
to: BED-01
priority: P1
status: Pending
created: 2026-11-19
title: SLOP cleanup — SMS steering + shared MemoryTape assembly
depends_on: PROP-18.10
---

# PROP-18.12 — BED hygiene (PROP-18.10 F1/F2)

**Activate:** `@Agents/BED-01.md`

1. **F1** — `SmsInboundService` preamble: call `MemorySteeringGuidance.AppendToPreamble` after dated `[Memory]` (same cue as desk).
2. **F2** — Stop hand-rolling SMS memory lines; use `MemoryTape.BuildMemoryBlock` (add optional SMS bullet/prefix if needed). One assembly path.

Tests green for desk + SMS. Report: `docs/agents/reports/PROP-18.12-BED01-to-PM01.md`
