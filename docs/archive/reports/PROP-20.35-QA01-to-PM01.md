---
type: report
prop_id: PROP-20.35
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
priority: P1
status: Completed
created: 2026-11-18
depends_on: PROP-20.34
verdict: Pass
title: QA PROP-20.34 SLOP hygiene (emotion dedupe + shared applies-when)
---

# PROP-20.35 — QA-01 → PM-01

**Result: Pass**

Verified PROP-20.34: desk chat emotion is a single snapshot per turn; Compass and context-trait applies-when share one scorer; trait / Compass selection semantics unchanged. Ready for SLOP-01.

## Acceptance matrix

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Single emotion snapshot path (no second `_emotion.GetAsync` for flat-affect label) | **Pass** | Source: only `LoadEmotionPreambleAndLabelSafeAsync` in `ChatContextBuilder`; label reused for `SelectContextTraits`. Old `LoadEmotionPreambleSafeAsync` / `LoadEmotionLabelSafeAsync` absent. Runtime: `ChatContextBuilderTests` + emotion suite green |
| 2 | Shared applies-when scorer (Compass + context traits) | **Pass** | Source: one `ScoreAppliesWhen`; `ScoreContextAppliesWhen` gone; both `SelectContextTraits` and `SelectCompassLines` call it. Runtime: VictoriaMindBlock + ChatContextBuilder selection tests green |
| 3 | Context-trait selection still correct (cap 3, stale omit, flat affect) | **Pass** | `SelectContextTraits_*`; `BuildAsync_FreshTopicalTurn_*`; `BuildAsync_StaleContact_*`; leisure context-trait inject |
| 4 | Compass selection still correct (no match omit, match ≤3, floors/resting remain) | **Pass** | `SelectCompassLines_*`; `BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines`; `BuildAsync_UnrelatedTurn_OmitsCompass_*` |
| 5 | Emotion influence / label behavior intact | **Pass** | `EmotionInfluencePromptTests` (4/4) |

## Static hygiene check (QA, no code edits)

| Check | Result |
| --- | --- |
| `LoadEmotionPreambleAndLabelSafeAsync` present; old dual loaders absent | Pass |
| `ScoreAppliesWhen` present; `ScoreContextAppliesWhen` absent | Pass |
| `ChatContextBuilder` desk path: one `_emotion.GetAsync` inside the combined loader | Pass |

Other `_emotion.GetAsync` call sites (leisure builder, snapshot sender, post-effects, soul loop) are out of PROP-20.34 desk-turn scope and were not treated as F1 regressions.

## Commands run

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~EmotionInfluencePromptTests" `
  --verbosity normal

dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~Prop17LeisureTests|FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder" `
  --no-build --verbosity normal
```

## Test evidence (real output)

Primary hygiene filter:

```
Test Run Successful.
Total tests: 35
     Passed: 35
 Total time: 0.5732 Seconds

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Broader regression (leisure + mind-block + chat context):

```
Test Run Successful.
Total tests: 55
     Passed: 55
 Total time: 0.8343 Seconds

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Selection / emotion Pass lines (re-listed):

```
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_NoMatch_OmitsContextSection_RestingPresent
Passed SoulCore.Protocol.Tests.EmotionInfluencePromptTests.High_arousal_mid_valence_is_alert_not_flat
Passed SoulCore.Protocol.Tests.EmotionInfluencePromptTests.BuildPreamble_IsDeterministic_AndDiffersByEmotion
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultCompassAppliesWhen_DocumentsFiveTitles
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ParallelReads_ComposesPreambleWithToolGuidance
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultContextTraits_SeedHasTopicHeatShadow_WithAppliesWhen
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_StaleContact_OmitsContextTraits_KeepsResting
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectCompassLines_NoMatch_ReturnsEmpty_BuildOmitsCompassSection
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_UnrelatedTurn_OmitsCompass_KeepsFloorsAndResting
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_MultiMatch_CapsAtThree
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_FreshTopicalTurn_InjectsAtMostThreeContextTraits
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_FlatAffect_ExcludesHeatAndShadow_AllowsTopic
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectCompassLines_MultiMatch_CapsAtThree_FloorsAndRestingRemain
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_FreshTopicalInterest_EarnsContextTraitsCapThree
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_MatchingInterest_EarnsCompassCapThree
```

## Issues

None. No new ISSUE files.

## Notes for PM

- PROP-20.34 F1/F2 verified; safe to archive PROP-20.34 + PROP-20.35 together after SLOP.
- **Ready for SLOP-01** (post-QA slop audit).
