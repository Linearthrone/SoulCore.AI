---
type: task
prop_id: PROP-20.3b
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-17
title: Apply Memory migration 012 (thoughts/wants/dreams) via Host restart
depends_on: PROP-20.3
upstream: docs/agents/reports/PROP-20.3-DBD01-to-PM01.md
accept: docs/agents/reports/PROP-20.3-PM01-accept.md
---

# PROP-20.3b — OPS apply journal migration 012

**Activate:** `@Agents/OPS-01.md` then execute.

## Change

DBD landed migration `012_thoughts_wants_dreams_journals` in `SoulCore.Memory`. It applies automatically when Host opens SQLite and `schema_migrations` lacks `012`.

## Action

1. Rebuild Host with current tree (includes Memory 012).
2. Restart Host so migration runs (home-pc / ALLSTART per runbook — do not invent a second deploy path).
3. Verify `schema_migrations` contains version `012` (sqlite query or Host log evidence).
4. Optional smoke: journal books list includes `thoughts`, `wants`, `dreams` via existing companion API if one exists.

## Do not

- Destructive rollback of 012
- Deploy unrelated Wave A tickets that are still in flight unless already on the same build

## Acceptance

- [ ] Host running on build that includes Memory 012
- [ ] Evidence `012` is in `schema_migrations` (or equivalent log)
- [ ] Report: `docs/agents/reports/PROP-20.3b-OPS01-to-PM01.md`
