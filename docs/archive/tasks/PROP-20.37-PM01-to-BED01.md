---
type: task
prop_id: PROP-20.37
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Done
created: 2026-11-18
title: Step 7 — bond counters + soak gate + growth delta rules
unblocks: PROP-20.38
decision: docs/agents/unexecuted_proposals/prop-20-growth-routing-decision.md
---

# PROP-20.37 — Growth Host (bond + soak + deltas)

**Activate:** `@Agents/BED-01.md`  
**Law:** `docs/agents/unexecuted_proposals/prop-20-growth-routing-decision.md`

## Do

1. **Bond counters** from Host facts (compute or persist as needed): contact time, days with contact, entrusted tasks, operator corrections, her initiated reaches, journal opens. Expose on Identity API (alongside restingTraits).
2. **Soak clock:** counters **display-only** for **14 days**; do **not** change prompt/routing until soak opens. Gate: ≥**3 days with contact** in the window or soak stays closed.
3. **Delta rules:** inside-band auto-apply + ledger; **band-cross → pending** until operator confirm. After soak: only **responsiveness / initiative / openness** may be nudged from counters (small inside-band first week after open).
4. Persist pending band-cross proposals + ledger reasons so FED can show/confirm (DBD migration OK if you need tables — document in report).
5. Tests: display-only before soak; no routing change pre-soak; inside-band applies; band-cross stays pending; contact gate.

## Out of scope

- Full Identity UI (FED-20.38)
- Step 8 second wave
- Changing Constitution floors from bond

## Report

`docs/agents/reports/PROP-20.37-BED01-to-PM01.md` with real test output.
