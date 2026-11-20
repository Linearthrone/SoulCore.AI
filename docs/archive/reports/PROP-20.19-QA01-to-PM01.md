---
type: report
prop_id: PROP-20.19
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.18
task: docs/agents/tasks/PROP-20.19-PM01-to-QA01.md
---

# PROP-20.19 — QA post-20.18 slop cleanup — Pass

## Verdict: **Pass**

Regression after PROP-20.18 is green. `CharterFloorMap` is gone from product `.cs` (file deleted; zero call sites). Charter / VictoriaMindBlock / Prop17Leisure filter suite: **49 passed / 0 failed**. Tests ran via alternate output dir (Host DLL lock avoided).

**Ready for SLOP-01** (post-QA slop / duplicate / alias delta).

## Acceptance

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | No `CharterFloorMap` product refs | **Pass** | `rg --glob "*.cs"` exit 1 (no matches); `CharterFloorMap.cs` absent under `SoulCore.Core/Charter/` |
| 2 | Charter / VictoriaMindBlock / Prop17Leisure filters | **Pass** | 49 passed, 0 failed (paste below) |
| 3 | Host DLL lock avoided | **Pass** | Built/ran with `-o .tmp\prop2019-test` (did not stop Host) |

## Commands run

```powershell
rg -n "CharterFloorMap" --glob "*.cs" .
# RG_EXIT=1 (no matches)

dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~CharterServiceTests|FullyQualifiedName~CharterFourBookMigrationTests|FullyQualifiedName~Prop17LeisureTests" `
  -o .tmp\prop2019-test `
  --verbosity normal
```

## CharterFloorMap check (pasted)

```
RG_EXIT=1 (1=no matches)
False   # Test-Path SoulCore\SoulCore.Core\Charter\CharterFloorMap.cs
# Charter dir still has CharterBooks.cs / CharterService.cs (no FloorMap)
```

Historical mentions remain only in `docs/agents/**` task/report archive text — not product code.

## Test output (pasted)

```
Test run for C:\Users\kurtw\.house_victoria\SoulCore.AI\.tmp\prop2019-test\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.65]   Discovering: SoulCore.Protocol.Tests
[xUnit.net 00:00:00.74]   Discovered:  SoulCore.Protocol.Tests
[xUnit.net 00:00:00.74]   Starting:    SoulCore.Protocol.Tests
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [102 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_FormatsTitlePlusFloorLine [102 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureSession_SelfStartsWithoutRelease [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_WithInjectedFloors_UsesVictoriaMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreSevenLockedNamesAtMid052 [6 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.CharterBooks_EighteenTitles_ExactlyOneBookEach [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers [3 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.TruncateFloorLine_CapsLongBodiesWithoutSentence [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_PrefersSchemaBookOverTitleMap [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_NeverReusesCloudBaseUrl [59 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_HonorsLocalEmbeddingOverride [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_SaysSoOnceUntilWorkMoves [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_UnfinishedWork_IsNotASuspendedProposal [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureModel_Default_IsQwen15b [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_RunnableTodoBlocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_WriteSuppressed_WithoutFloorsOrResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureTools_DenyHostRejects [2 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_BuyEmailInstallTrade_Classifies [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_SuspendedDoesNotBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureModel_Empty_MeansQuiet_NeverWorkModelFallback [< 1 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_InsertsRows_AndGetAnchorsReturnsAll [327 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.GetAnchorsAsync_EmptyDb_ReturnsEmptyList [57 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_InvalidKind_Throws [64 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.GetAnchorsByKindAsync_FiltersByKind [66 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_EighteenLiveTitles_ExactlyOneBookEach_MatchingFloorMap [66 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_EmptyList_ReturnsZero [68 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.ListAnchorDetailsAsync_ReturnsStructuredRows_FilterableByKind [74 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.GetAnchorsByKindAsync_InvalidKind_Throws [68 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_LandsSuspended_WithoutStartNow [631 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_InProgressLiveSurface_Blocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_SmallModelFailure_SelectsDeskChatModel [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndSevenResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.CharterFourBookMigrationTests.Migration013_RejectsUnknownBook_ViaCheckConstraint [803 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_Excuse_IsNotAResearchFind [8 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.UntrustedObservation_CannotPoisonPreamble [5 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_TodoWithDeadSurface_AllowsRr [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.InferenceLease_Exclusive_AndAbort [< 1 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_EmptyBody_Throws [56 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_UnknownBook_Throws [58 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_RollsBackOnFailure [72 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.GetAnchorsByKindAsync_LockedOnly_FiltersByIsLocked [72 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_EmptyTitle_Throws [47 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.CharterService_SeedAndRead_RoundTrip [64 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.Constructor_NullOrEmptyPath_Throws [50 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_InvalidSource_Throws [54 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_PriorityOrdering_Ascending [76 ms]
  Passed SoulCore.Protocol.Tests.CharterServiceTests.ListAnchorDetailsAsync_UnknownBook_Throws [62 ms]
  Passed SoulCore.Protocol.Tests.CharterFourBookMigrationTests.Migration013_MapsEighteenLiveTitles_ToFloorMapBooks [723 ms]
[xUnit.net 00:00:02.30]   Finished:    SoulCore.Protocol.Tests

Test Run Successful.
Total tests: 49
     Passed: 49
 Total time: 2.9417 Seconds

Build succeeded.
    5 Warning(s)
    0 Error(s)
Time Elapsed 00:00:07.78
```

## Notes

- Same filter set as PROP-20.18 BED evidence (49 tests).
- Warnings only: SixLabors Debug license notice + existing CA1416 platform warnings — not regressions.
- No new ISSUE filed.

## Handoff

PM-01: accept → dispatch **SLOP-01** delta on 20.18 surface → then step-4 archive when slop clears.
