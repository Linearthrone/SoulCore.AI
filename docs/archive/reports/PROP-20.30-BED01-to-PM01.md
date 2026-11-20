---
type: report
prop_id: PROP-20.30
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
title: Step 5 — trait framework + Host context-trait selection (≤3)
---

# PROP-20.30 — BED-01 → PM-01

**Result: Pass**

Host-owned context-trait framework shipped. Resting traits stay always-on. Host selects at most three extra (context) trait lines via `applies-when` tags when contact is &lt;24h fresh; model never picks. Flat affect (`calm`/`neutral`) allows topic only (heat/shadow excluded). Identity API exposes resting + catalog for FED-20.31. No bond/soak/routing (step 7).

## Law checklist

| Rule | Status |
| --- | --- |
| Resting always on (nine locked + operator weights) | Pass |
| Host selects ≤3 context traits by tags | Pass |
| ≥24h newest contact → zero context | Pass |
| Flat affect → topic OK; heat/shadow out | Pass |
| No model-picked traits / no embedding index | Pass |
| Dated memory still fits 16k budget | Pass (smoke) |
| Step 7 soak/bond routing | Out of scope (not done) |

## What shipped

1. **v1 context-trait schema** (`VictoriaMindBlock.ContextTraitLine`): id, name, group, kind (Topic/Heat/Shadow), weight, band, prompt line, `applies-when`, ledger reason.
2. **Seed catalog** (`DefaultContextTraits`): Curious, Analytical, Playful, Assertive (topic); Volatile (heat); Dark (shadow) — enough to prove selection; not full second-wave fill.
3. **`SelectContextTraits`**: match → ≤3; no match / stale / empty → empty; flat affect filters heat/shadow.
4. **`VictoriaMindBlock.Build`**: optional `[Context traits]` section between Compass and Resting (omitted when empty).
5. **Consumers**: `ChatContextBuilder`, `VictoriaMindBlockBuilder` (SMS), `LeisureMindBlock` — shared select path.
6. **`GET /settings/identity`**: `restingTraits[]`, `contextTraits[]` (empty on bare GET — turn-scoped), `contextTraitCatalog[]` for FED-20.31.

## Identity API (FED-20.31)

Endpoint: `GET /settings/identity`

New fields:

```json
{
  "restingTraits": [
    {
      "id": "dominant",
      "name": "Dominant",
      "group": "resting",
      "weight": 0.7,
      "band": "Mid",
      "promptLine": "...",
      "ledgerReason": "operator"
    }
  ],
  "contextTraits": [],
  "contextTraitCatalog": [
    {
      "id": "curious",
      "name": "Curious",
      "group": "cognitive",
      "kind": "topic",
      "weight": 0.52,
      "band": "Mid",
      "promptLine": "...",
      "appliesWhen": ["curious", "research", "..."],
      "ledgerReason": "operator-provisional-until-seed"
    }
  ]
}
```

- **restingTraits** — always the nine locked rows (weight / band / ledgerReason) for the Identity panel.
- **contextTraits** — selected-for-this-turn; bare settings GET returns `[]` (selection is chat/SMS/leisure turn-scoped).
- **contextTraitCatalog** — v1 seed shape so FED can show available kinds without inventing rows.

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | Schema, seed, SelectContextTraits, fresh/flat helpers, Build section |
| `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` | Fresh gate + emotion label + context wire |
| `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` | Memory newest contact + emotion + SelectContextTraits |
| `SoulCore/SoulCore.Host/Companion/IVictoriaMindBlockBuilder.cs` | Docs |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | Optional contact/emotion → SelectContextTraits |
| `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` | Identity resting/context exposure |
| `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` | Fresh/stale/cap/flat/budget tests |
| `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` | Desk fresh/stale integration |
| `SoulCore/SoulCore.Protocol.Tests/Prop17LeisureTests.cs` | Leisure context wire |

## Test evidence (real output)

Command:

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~Prop17Leisure" `
  --verbosity normal
```

Summary:

```
Passed!  - Failed:     0, Passed:    55, Skipped:     0, Total:    55, Duration: 396 ms - SoulCore.Protocol.Tests.dll (net8.0)
```

PROP-20.30-focused Pass lines:

```
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_NoMatch_OmitsContextSection_RestingPresent
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.IsContactFresh_NullOrStale_False_Recent_True
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultContextTraits_SeedHasTopicHeatShadow_WithAppliesWhen
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_StaleContact_ReturnsEmpty_RestingStillInBuild
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_MultiMatch_CapsAtThree
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_FlatAffect_ExcludesHeatAndShadow_AllowsTopic
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_ContextTraits_StayInsideBudgetWithMemorySmoke
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_FreshTopicalTurn_InjectsAtMostThreeContextTraits
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_StaleContact_OmitsContextTraits_KeepsResting
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_FreshTopicalInterest_EarnsContextTraitsCapThree
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors
```

## Out of scope (confirmed not done)

- Bond counters / soak / band-crossing confirm (step 7)
- Second-wave group fill-out (step 8)
- Changing the six Mid provisional resting weights
- OPS / FED UI beyond Host Identity API fields

## Handoff

- **FED-20.31** unblocked for Identity panel (weight / band / ledgerReason from `restingTraits`).
- PM may Accept and release FED hold on PROP-20.31.
