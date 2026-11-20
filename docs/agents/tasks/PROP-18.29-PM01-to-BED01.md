---
type: task
prop_id: PROP-18.29
from: PM-01
to: BED-01
priority: P2
status: Pending
created: 2026-11-19
title: SLOP cleanup — ActivityLogDto alias + Composer reuse
depends_on: PROP-18.25
---

# PROP-18.29 — BED hygiene (PROP-18.25 F1–F2)

**Activate:** `@Agents/BED-01.md`

1. **F1** — `ActivityLogDto`: keep `desc` only (drop `sessionDesc` alias) unless FED already depends — if so, document one canonical field and update client.
2. **F2** — Writer: reuse Composer paragraph-floor helper; no re-split twin.

Tests green. Report: `docs/agents/reports/PROP-18.29-BED01-to-PM01.md`
