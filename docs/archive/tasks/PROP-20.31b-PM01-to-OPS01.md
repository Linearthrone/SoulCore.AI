---
type: task
prop_id: PROP-20.31b
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-18
title: Presence Release rebuild for Identity trait panel
depends_on: PROP-20.31
related: PROP-20.30b
---

# PROP-20.31b — OPS Presence Release (FED-20.31)

**Activate:** `@Agents/OPS-01.md`

FED-20.31 Accepted but ChatDesktop was built to an **isolated** out path (live Release bin locked). Host may also be mid-**20.30b** restart — **serialize**: do not overlap ForceRebuild with another ALLSTART.

1. If `:7710` down / no `restingTraits` yet — finish or wait for **20.30b** Host Release first.
2. Stop ChatDesktop, rebuild `House.ChatDesktop` **Release** into normal `bin\Release`, start against `127.0.0.1:7710`.
3. Confirm Identity can load (Host `/settings/identity` has `restingTraits`).

Report: `docs/agents/reports/PROP-20.31b-OPS01-to-PM01.md`
