---
type: task
prop_id: PROP-20.27
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-18
title: Compass in preamble — retrieve like traits, cap 3 (choice B)
decision: docs/agents/unexecuted_proposals/prop-20-compass-prompt-decision.md
---

# PROP-20.27 — Compass retrieve (cap 3)

**Activate:** `@Agents/BED-01.md`

## Law

Operator chose **B**: Compass book lines enter the mind block only when earned this turn, **≤3**. Never dump full Compass. Constitution floors + resting stay always-on.

## Solution

1. Load Compass rows from charter `book=compass` (five titles from floor map).
2. v1 Host match: `applies-when` tags (or equivalent) against user text / leisure interest — same spirit as extra traits. If tags are missing on rows, add a minimal tag set per Compass title (document in report) so matching works.
3. Cap **3** lines in the preamble; zero if no match.
4. Wire into `VictoriaMindBlock` / `ChatContextBuilder` / SMS+leisure mind builders that share the block.
5. Tests: no match → no Compass section; multi-match → ≤3; floors+resting still present.

## Do not

- Always-on Compass top-by-weight (rejected choice A)
- Full step 5 trait table / context-trait Host engine beyond what’s needed for Compass match
- Shadow traits

## Acceptance

- [ ] Fresh unrelated turn: no Compass lines in preamble
- [ ] Matching turn: at most three Compass lines
- [ ] Tests + report: `docs/agents/reports/PROP-20.27-BED01-to-PM01.md`
