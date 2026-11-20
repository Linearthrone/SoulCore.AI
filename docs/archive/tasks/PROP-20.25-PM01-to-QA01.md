---
type: task
prop_id: PROP-20.25
from: PM-01
to: QA-01
priority: P0
status: Pending
created: 2026-11-17
title: QA operator resting weights + Release smoke after 20.1d
depends_on: PROP-20.24
soft_depends: PROP-20.1d
---

# PROP-20.25 — QA weights + Release

**Activate:** `@Agents/QA-01.md`

1. Unit: Dominant 0.70 / Ambitious 0.80 / Uninhibited 0.80 in DefaultRestingTraits — paste tests.
2. After OPS Release: `/health`, `/settings/identity`, `/journals/books` on live Host.
3. Do not claim Pass if Host is still Debug-only.

Report: `docs/agents/reports/PROP-20.25-QA01-to-PM01.md`
