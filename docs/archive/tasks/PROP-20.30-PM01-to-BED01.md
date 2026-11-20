---
type: task
prop_id: PROP-20.30
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-18
title: Step 5 — trait framework + Host context-trait selection (≤3)
unblocks: PROP-20.31
---

# PROP-20.30 — Trait framework (Host)

**Activate:** `@Agents/BED-01.md`

## Law (plan step 5)

- Resting traits always on (nine locked + operator weights already in `VictoriaMindBlock.DefaultRestingTraits`).
- Host picks **at most three** extra (context) trait lines when `applies-when` matches this turn / leisure interest.
- **Fresh rule:** if newest chat episode or SMS turn is ≥24h old, add **zero** context traits.
- Flat affect on a live thread: topic traits OK; heat/shadow traits do not load when Host label is `calm`/`neutral` (arousal & valence rule in plan).
- Model does **not** pick traits. No new embedding index in v1 — tags only.
- Dated memory block still fits inside `ContextPreambleCharLimit` (16,000).

## Do

1. Introduce a v1 personality / context-trait table shape (code schema): id/name, group, weight, band, prompt line, `applies-when` tags, ledger reason. Seed a small non-resting set enough to prove selection (do not invent growth/routing — that is step 7).
2. `SelectContextTraits` (or equivalent) shared like Compass: match → ≤3; no match → empty; wire into `VictoriaMindBlock.Build` + desk/SMS/leisure consumers.
3. Enforce 24h fresh gate in Host builders (wall-clock on newest contact).
4. Expose resting + selected context trait rows (weight, band, ledger reason) on Identity settings API so FED-20.31 can render (extend GET `/settings/identity` or adjacent read-only endpoint — document in report).
5. Tests: fresh/stale turn; match caps at 3; no match omits context section; resting always present; budget smoke.

## Out of scope

- Bond counters / soak / band-crossing confirm (step 7)
- Second-wave groups fill-out (step 8)
- Changing the six Mid provisional resting weights
- PM / OPS / FED UI work

## Report

`docs/agents/reports/PROP-20.30-BED01-to-PM01.md` with real `dotnet test` output.
