---
type: task
prop_id: PROP-20.41
from: PM-01
to: BED-01
priority: P2
status: Done
created: 2026-11-18
title: SLOP cleanup — GrowthService soak helper + seed SoT
depends_on: PROP-20.40
---

# PROP-20.41 — BED hygiene (PROP-20.40 F1/F2)

**Activate:** `@Agents/BED-01.md`

1. **F1** — Dedupe soak/gate eval in `GrowthService` into one helper (e.g. `ResolveSoakAsync`); Identity may still do a second counter compute for display-to-now.
2. **F2** — `GrowthTraits.DefaultSeedTraits` is **single source of truth**. Keep migration 014 schema; either drop duplicate prose from SQL seed / seed from C# on empty, or add a parity test that fails if SQL INSERT text diverges. Do not leave silent dual edits.

Tests green. Report: `docs/agents/reports/PROP-20.41-BED01-to-PM01.md`
