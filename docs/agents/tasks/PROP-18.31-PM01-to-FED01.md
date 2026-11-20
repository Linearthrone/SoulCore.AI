---
type: task
prop_id: PROP-18.31
from: PM-01
to: FED-01
priority: P1
status: Pending
created: 2026-11-19
title: Desk hygiene — drop dead topic client + honesty journal label
depends_on: PROP-18.27
---

# PROP-18.31 — FED hygiene (PROP-18.27 F1–F2)

**Activate:** `@Agents/FED-01.md`

**PM decisions (locked):**

1. **F1** — Remove unused `ListTopicAsync` / `ParseTopicJournal` / `TopicJournalFetch` / related DTOs from `SoulCoreActivitiesClient` until a real desk caller exists.
2. **F2** — Honesty band “Last journal” must not fall back to activity Desc. Empty journal crumb → `none yet`.

Update tests. Report: `docs/agents/reports/PROP-18.31-FED01-to-PM01.md`
