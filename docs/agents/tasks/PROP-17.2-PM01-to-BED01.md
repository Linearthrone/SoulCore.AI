---
type: task
prop_id: PROP-17.2
prop_root: PROP-17-victoria-between-talks
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-10-12
updated: 2026-10-12
wave: between-talks
title: Tab/browser invariants + SOP store + life-hacks journal
depends_on: none (parallel with 17.1)
blocks: PROP-17.3 (hard — tab/browser refuse must exist)
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
intake: docs/agents/tasks/PROP-17-TT01-to-PM01.md
urgency: same-day — stop duplicate tabs / second browser before R&R
environment: TINA-main
---

# PROP-17.2 — Browser hygiene, SOP store, life-hacks book

**Activate:** `@Agents/BED-01.md` then `Start PROP-17.2.` (parallel with 17.1 OK — disjoint enough).

## Operator urgency

Same-day Victoria bring-up. She already opens many tabs to the same place and installed a second browser. **Code gates today**, not a document she can skip. Diligent tests; hurried land.

## Problem

No hard invariant for one live tab per URL or “refuse second browser.” Journals CHECK only `feeling|animation|environment`. No SOP store — hygiene lived in prompt text (kill).

## Solution

### A. Browser / guest invariants (code)

1. **One live tab per URL** — reuse existing `browser_tabs` / guest browser tooling; navigating or opening an already-open URL focuses the existing tab.
2. **Refuse a second browser** when one is already open (Firefox/Chrome/etc. in guest).
3. **Refuse install-a-browser** (ties to 17.1 `guest_shell` package refuse; also block browser-install intent paths if separate).
4. Site down → signal park path (17.1), not refresh-loop. If 17.1 park API is not merged yet, expose a clear failure code 17.1 can consume — do not invent a second park system.

### B. SOP store

1. New table (charter-shaped): title, when, steps, don’ts; CRUD tools as needed.
2. Seed two SOPs: (1) browser hygiene / one tab per URL / no second browser; (2) site down means park.
3. Authority order (document in handbook note or code comment): tool code → charter → operator settings → her notes as recall.
4. An SOP **cannot** flip a gate, mint `confirmed`, or call `guest_shell`.

### C. Life-hacks journal

1. Migration: extend journal `CHECK` to include `life_hacks` (fourth book).
2. Do not pretend `feeling` is that book.

## Do not

- R&R / leisure model (17.3)
- Presence shell (17.4)
- Prompt-only SOPs as the only enforcement
- Full guest binary allowlist / snapshot rollback (out of scope)

## Acceptance

- [ ] Opening an already-open URL reuses the tab (test or guest probe evidence)
- [ ] Second browser open refused when one is live
- [ ] Browser install refused (with 17.1 package gate)
- [ ] SOP table + two seeds; SOP cannot override host gates
- [ ] `life_hacks` journal kind migrates cleanly; tests updated
- [ ] Report: `docs/agents/reports/PROP-17.2-BED01-to-PM01.md`

## Handoff

Parallel with 17.1. Both Pass → PM releases **PROP-17.3**.
