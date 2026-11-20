---
type: report
prop_id: PROP-18.2
prop_root: PROP-18-chronology-and-felt-persistence
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
depends_on: PROP-18.1
---

# PROP-18.2 — Daybook bridge — Pass

## Verdict: **Pass**

Desk chat now injects a provenance-tagged `[Daybook]` block (recent journals + optional activity-log crumbs) inside the identity/memory budget, ahead of `[Memory]`. Soft caps keep leisure notes from flooding. Optional `recall_timeline` tool for on-demand chronology. Private journals stay off SMS (unchanged PROP-20.4 contract).

## Done

1. **`DaybookTape`** — formats `provenance | occurred_at | text`; preferred books (`feeling` / `life_hacks` / `thoughts` / `wants` / `dreams`) today-first; leisure source → `journal:{book}/leisure`; activity crumbs → `activity-log`; body truncate 280 chars; ≤5 journal + ≤3 crumbs; soft 2400-char daybook budget.
2. **`ChatContextBuilder`** — parallel load of journals + activities log; `[Daybook]` placed after mind block, before `[Memory]`, sharing the 16k budget (daybook ≤⅓ of remaining so episodic recall is not starved).
3. **`recall_timeline` tool** — cheap store reads (journal ± book filter, activity-log, episodic); windows `recent` / `today` / `week`; registered in memory DI.
4. **Tests** — format/provenance, ordering, tight-budget drop, preamble inject order, live SQLite BuildAsync inject, tool output.

## Out of scope (honored)

- SMS daybook (private journals never ride SMS)
- PROP-18.7 residue writers / depth law
- PROP-18.3 Presence HUD / while-you-were-gone
- Voice wake (Q4 locked No)

## Acceptance

| Criterion | Result |
| --- | --- |
| Recent dated journal in chat preamble / `[Daybook]` | Pass |
| Provenance tags; leisure does not flood memory budget | Pass |
| Optional activity-log crumbs | Pass |
| Optional `recall_timeline` when cheap | Pass |
| Tests with real output | Pass — below |

## Files

- `SoulCore/SoulCore.Host/Ws/DaybookTape.cs` (new)
- `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs`
- `SoulCore/SoulCore.Inference/Tools/RecallTimelineTool.cs` (new)
- `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/MemoryServiceCollectionExtensions.cs`
- `SoulCore/SoulCore.Protocol.Tests/DaybookTapeTests.cs` (new)
- `SoulCore/SoulCore.Inference/Presence/ActivityResidueComposer.cs` (unrelated compile fix: `SplitParagraphs` → `List<string>` so Host builds)

## Evidence (`dotnet test`)

Filter: `DaybookTapeTests|ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion|ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock`

```
Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9
```

Sample daybook line:

```
journal:feeling | 2026-11-19T08:00:00.000Z | felt curious about chronology
activity-log | 2026-11-19T09:15:00.000Z | research / PROP-18.2 — Sketched daybook provenance tags so leisure notes stay short.
```

Preamble order verified: `[Identity]` → mind → `[Daybook]` → `[Memory]` → steering → `[SoulCore emotion]`.

## Next

PROP-18.7 writers feed this bridge; PROP-18.3 FED can surface last journal crumb from the same stores.
