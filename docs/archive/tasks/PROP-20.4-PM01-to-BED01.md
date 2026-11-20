---
type: task
prop_id: PROP-20.4
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Done
created: 2026-11-17
title: One mind block on chat — floors + resting Mid defaults + shared builder
depends_on: none
siblings: PROP-20.2, PROP-20.5, PROP-20.6, PROP-20.7
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.4 — Mind block on desk chat (+ shared builder)

**Activate:** `@Agents/BED-01.md` then execute this ticket.

## Problem

Every pass that speaks as her must carry one mind block: Constitution floors (titles + floor line), resting traits, and related memories. Resting **names** are locked; seed **weights** are Open (block step 5 only). Ship seven resting lines at Mid-band defaults until the operator sets seeds.

## Resting set (locked names)

Dominant, Possessive, Affectionate, Driven, Devoted, Intense, Uninhibited.

Use Mid band midpoint weights (~0.52) and short prompt lines. Ledger reason: `operator-provisional-until-seed`. Do **not** add Ambitious/Proud until Open closes. Do **not** implement context-trait Host selection (step 5).

## Solution

1. Extract or add a shared mind-block builder usable by chat (and later SMS/leisure callers).
2. Wire desk chat compose so the preamble includes floors + resting lines inside the existing 16,000-char identity/memory budget (`ContextPreambleCharLimit`). Emotion stays outside.
3. Channel text remains beside the mind block, not instead of it.
4. Keep Kayleigh address line until second wave.
5. Tests: fresh compose contains resting lines; budget still holds with memory section present.

## Files to change

- `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` (identity/mind preamble — coordinate with 20.2 on memory helpers only)
- New small helper under Host or Inference if needed for reuse
- Protocol tests for preamble content

## Do not touch

- `LeisureRunner.cs` / `LeisureTools.cs` (20.6)
- SMS Companion inbound/outbound (20.5 / 20.7)
- Journal schema (20.3)
- Full trait table / band engine (held step 5)

## Acceptance

- [ ] Desk chat preamble contains Constitution floors + seven resting lines
- [ ] Shared builder callable without copying paste into SMS/leisure tickets
- [ ] 16k budget still enforced; emotion outside
- [ ] Tests + report: `docs/agents/reports/PROP-20.4-BED01-to-PM01.md`
