---
type: task
prop_id: PROP-15.19
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: BED-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
report: docs/agents/reports/PROP-15.19-BED01-to-PM01.md
depends_on: none
ops_report: docs/agents/reports/PROP-15.17b-OPSHOME-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: PUT /api/personas/{id} must merge — not wipe on partial body
---

# PROP-15.19 — Partial PUT must not wipe pack

## Problem

OPS 15.17b: PUT with only `inferenceModel` (or sparse body) **wiped** lexi pack fields; had to restore from C3 dump. Data-loss / feel-drive hazard.

## Solution

`PUT /api/personas/{id}`: load existing pack, merge provided fields, persist. Reject empty body. Never replace with defaults for omitted properties. Tests: PUT `{ inferenceModel }` keeps IdentityBlurb / traits / titles.

## Acceptance

- [x] Partial PUT preserves omitted fields
- [x] Tests + report `docs/agents/reports/PROP-15.19-BED01-to-PM01.md`
