---
type: task
prop_id: PROP-20.38b
from: PM-01
to: OPS-01
priority: P0
status: Done
created: 2026-11-18
title: Presence Release rebuild for Identity growth panel
depends_on: PROP-20.38
related: PROP-20.37b
---

# PROP-20.38b — OPS Presence Release (FED-20.38)

**Activate:** `@Agents/OPS-01.md`

FED built to temp out (ChatDesktop locked). Serialize with **20.37b**. After Host has migration 014 + `bondCounters`/`soak` on Identity: stop ChatDesktop, rebuild Presence **Release** into normal `bin\Release`, start against `:7710`.

Report: `docs/agents/reports/PROP-20.38b-OPS01-to-PM01.md`
