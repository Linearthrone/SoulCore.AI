---
type: task
prop_id: PROP-20.38
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: FED-01
priority: P1
status: Pending
created: 2026-11-18
title: Step 7 — Identity bond counters + pending band-cross confirm
depends_on: PROP-20.37
unblocked_by: docs/agents/reports/PROP-20.37-PM01-accept.md
---

# PROP-20.38 — Identity growth panel (FED)

**Activate:** `@Agents/FED-01.md` — PROP-20.37 Accepted. API: `bondCounters`, `soak`, `growthTraits`, `pendingBandCross`, `growthLedger`; confirm/dismiss `POST /settings/identity/growth/pending/{id}/confirm|dismiss`.

## Do

1. Identity tab shows bond counters from Host (read-only) + soak state (display-only vs open / days remaining if exposed).
2. Show **pending band-cross** trait deltas; operator can **confirm** or **dismiss** (wire to Host APIs from 20.37).
3. No fabricated counters; Host unreachable stays unreachable.
4. Build + smoke evidence.

## Out of scope

- Editing bond facts by hand
- Step 8

## Report

`docs/agents/reports/PROP-20.38-FED01-to-PM01.md`
