---
type: report
prop_id: PROP-20.22
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.21
task: docs/agents/tasks/PROP-20.22-PM01-to-QA01.md
---

# PROP-20.22 — QA nine resting traits (+Ambitious +Proud) — Pass

## Verdict: **Pass**

`DefaultRestingTraits` has all **nine** locked names at **0.52 Mid**, including **Ambitious** and **Proud**. VictoriaMindBlock / ChatContextBuilder / Prop17Leisure filter suite: **39 passed / 0 failed**. Host DLL lock avoided via `-c Release -o .tmp\prop2022-test`.

**Ready for SLOP-01** (post-QA slop / duplicate / alias delta).

## Acceptance

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Nine resting names at 0.52 Mid (incl. Ambitious, Proud) | **Pass** | `DefaultRestingTraits_AreNineLockedNamesAtMid052`; source list Dominant…Proud at Mid 0.52 |
| 2 | VictoriaMindBlock / ChatContextBuilder / Prop17Leisure evidence | **Pass** | Filter: 39 passed, 0 failed (paste below) |
| 3 | Host DLL lock avoided | **Pass** | `-c Release -o .tmp\prop2022-test` (did not stop Host) |

## Resting set verified

| Name | Weight | Band |
| --- | --- | --- |
| Dominant | 0.52 | Mid |
| Possessive | 0.52 | Mid |
| Affectionate | 0.52 | Mid |
| Driven | 0.52 | Mid |
| Devoted | 0.52 | Mid |
| Intense | 0.52 | Mid |
| Uninhibited | 0.52 | Mid |
| Ambitious | 0.52 | Mid |
| Proud | 0.52 | Mid |

## Commands run

```powershell
dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj -c Release `
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~Prop17LeisureTests" `
  -o .tmp\prop2022-test `
  --verbosity normal
```

## Test output (pasted)

```
Test run for C:\Users\kurtw\.house_victoria\SoulCore.AI\.tmp\prop2022-test\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.59]   Discovering: SoulCore.Protocol.Tests
[xUnit.net 00:00:00.67]   Discovered:  SoulCore.Protocol.Tests
[xUnit.net 00:00:00.67]   Starting:    SoulCore.Protocol.Tests
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_FormatsTitlePlusFloorLine [101 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [102 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder [101 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureSession_SelfStartsWithoutRelease [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_WithInjectedFloors_UsesVictoriaMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesAtMid052 [6 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.CharterBooks_EighteenTitles_ExactlyOneBookEach [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers [3 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.TruncateFloorLine_CapsLongBodiesWithoutSentence [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_PrefersSchemaBookOverTitleMap [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_NeverReusesCloudBaseUrl [37 ms]
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
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ParallelReads_ComposesPreambleWithToolGuidance [46 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.InsertBeforeEmotion_PlacesKnownProceduresAheadOfEmotion [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsMatchingSopIntoPreamble [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ContinuesWhenCharterFails [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_LandsSuspended_WithoutStartNow [398 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_InProgressLiveSurface_Blocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_SmallModelFailure_SelectsDeskChatModel [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_Excuse_IsNotAResearchFind [8 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.UntrustedObservation_CannotPoisonPreamble [3 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_TodoWithDeadSurface_AllowsRr [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.InferenceLease_Exclusive_AndAbort [< 1 ms]
[xUnit.net 00:00:01.26]   Finished:    SoulCore.Protocol.Tests

Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 1.7955 Seconds

Build succeeded.
    4 Warning(s)
    0 Error(s)
```

## Key cases

```
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesAtMid052 [6 ms]
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndNineResting [< 1 ms]
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [102 ms]
```

## Notes

- Same filter set as PROP-20.21 BED evidence (39 tests).
- Warnings only: existing CA1416 platform warnings — not regressions.
- No new ISSUE filed.
- Operator seed weights still provisional (`operator-provisional-until-seed`); Mid 0.52 is the locked default until seed lands.

## Handoff

PM-01: accept → dispatch **SLOP-01** delta on 20.21 surface → continue PROP-20 mind-path chain.
