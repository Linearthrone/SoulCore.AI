---
type: report
prop_id: PROP-20.28
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
status: Pass
created: 2026-11-18
depends_on: PROP-20.27
task: docs/agents/tasks/PROP-20.28-PM01-to-QA01.md
---

# PROP-20.28 — QA Compass retrieve B (cap 3) — Pass

## Verdict: **Pass**

Compass choice B verified: no match → no `[Compass]` section; match → ≤3 Compass lines; floors + resting still present on VictoriaMindBlock, ChatContextBuilder, and leisure. Filter suite **45 passed / 0 failed**.

**Ready for SLOP-01** (post-QA slop on PROP-20.27 Compass retrieve delta).

## Acceptance

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | No match → no Compass | **Pass** | `SelectCompassLines_NoMatch_ReturnsEmpty_BuildOmitsCompassSection`; `BuildAsync_UnrelatedTurn_OmitsCompass_KeepsFloorsAndResting`; leisure shared block omits Compass |
| 2 | Match → ≤3 Compass lines | **Pass** | `SelectCompassLines_MultiMatch_CapsAtThree_FloorsAndRestingRemain`; `BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines`; `LeisureMindBlock_MatchingInterest_EarnsCompassCapThree` |
| 3 | Floors + resting still present | **Pass** | Same three suites assert floors/resting markers on omit and match paths |

## Commands run

```powershell
dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj -c Release `
  --filter "FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~Prop17Leisure" `
  -o .tmp\prop2028-test `
  --verbosity normal
```

## Test output (pasted)

```
Test run for C:\Users\kurtw\.house_victoria\SoulCore.AI\.tmp\prop2028-test\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.93]   Discovering: SoulCore.Protocol.Tests
[xUnit.net 00:00:01.06]   Discovered:  SoulCore.Protocol.Tests
[xUnit.net 00:00:01.07]   Starting:    SoulCore.Protocol.Tests
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder [142 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_FormatsTitlePlusFloorLine [141 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [143 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureSession_SelfStartsWithoutRelease [1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_WithInjectedFloors_UsesVictoriaMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.CharterBooks_EighteenTitles_ExactlyOneBookEach [2 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights [8 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultCompassAppliesWhen_DocumentsFiveTitles [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers [3 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectCompassLines_NoMatch_ReturnsEmpty_BuildOmitsCompassSection [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.TruncateFloorLine_CapsLongBodiesWithoutSentence [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_PrefersSchemaBookOverTitleMap [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectCompassLines_MultiMatch_CapsAtThree_FloorsAndRestingRemain [1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_NeverReusesCloudBaseUrl [64 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_HonorsLocalEmbeddingOverride [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_SaysSoOnceUntilWorkMoves [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndNineResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_UnfinishedWork_IsNotASuspendedProposal [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureModel_Default_IsQwen15b [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_RunnableTodoBlocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_WriteSuppressed_WithoutFloorsOrResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureTools_DenyHostRejects [2 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_BuyEmailInstallTrade_Classifies [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_SuspendedDoesNotBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureModel_Empty_MeansQuiet_NeverWorkModelFallback [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_MatchingInterest_EarnsCompassCapThree [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ParallelReads_ComposesPreambleWithToolGuidance [79 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.InsertBeforeEmotion_PlacesKnownProceduresAheadOfEmotion [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_UnrelatedTurn_OmitsCompass_KeepsFloorsAndResting [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsMatchingSopIntoPreamble [2 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ContinuesWhenCharterFails [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_LandsSuspended_WithoutStartNow [455 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_InProgressLiveSurface_Blocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_SmallModelFailure_SelectsDeskChatModel [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_Excuse_IsNotAResearchFind [11 ms]
[xUnit.net 00:00:01.80]   Finished:    SoulCore.Protocol.Tests
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.UntrustedObservation_CannotPoisonPreamble [4 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_TodoWithDeadSurface_AllowsRr [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.InferenceLease_Exclusive_AndAbort [< 1 ms]

Test Run Successful.
Total tests: 45
     Passed: 45
 Total time: 2.6298 Seconds
```

## Compass-focused cases (subset)

| Suite | Test | Result |
| --- | --- | --- |
| VictoriaMindBlock | `SelectCompassLines_NoMatch_ReturnsEmpty_BuildOmitsCompassSection` | Passed |
| VictoriaMindBlock | `SelectCompassLines_MultiMatch_CapsAtThree_FloorsAndRestingRemain` | Passed |
| VictoriaMindBlock | `DefaultCompassAppliesWhen_DocumentsFiveTitles` | Passed |
| ChatContextBuilder | `BuildAsync_UnrelatedTurn_OmitsCompass_KeepsFloorsAndResting` | Passed |
| ChatContextBuilder | `BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines` | Passed |
| ChatContextBuilder | `BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality` | Passed |
| Prop17Leisure | `LeisureMindBlock_MatchingInterest_EarnsCompassCapThree` | Passed |
| Prop17Leisure | `LeisureMindBlock_UsesSharedVictoriaMindBlock` (no Compass on bare interest) | Passed |

## Issues

None.

## Handoff

- PM-01: accept Pass → dispatch **SLOP-01** on PROP-20.27 Compass retrieve delta (and OPS Release refresh if still open via PROP-20.27b).
