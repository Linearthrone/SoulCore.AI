---
type: report
prop_id: PROP-20.25
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.24
soft_depends: PROP-20.1d
task: docs/agents/tasks/PROP-20.25-PM01-to-QA01.md
---

# PROP-20.25 — QA operator weights + Release smoke — Pass

## Verdict: **Pass**

Unit: Dominant **0.70 Mid** / Ambitious **0.80 High** / Uninhibited **0.80 High** in `DefaultRestingTraits` — filter suite **39 passed / 0 failed**. Live Host is **Release** (`bin\Release\net8.0`) on `127.0.0.1:7710`; `/health`, `/settings/identity`, `/journals/books` all **200**. Soft-waited for PROP-20.1d; OPS report not yet filed, but Release process + probes are green.

**Ready for SLOP-01** (post-QA slop on PROP-20.24 operator-weight delta; 20.23 SLOP predated the bake).

## Acceptance

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Dominant 0.70 / Ambitious 0.80 / Uninhibited 0.80 in DefaultRestingTraits | **Pass** | `DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights`; Build/p preamble Contains |
| 2 | Live Release `/health`, `/settings/identity`, `/journals/books` | **Pass** | All 200 on `:7710` after soft-wait; Host CMD is Release (not Debug) |
| 3 | Do not Pass if Debug-only | **Pass** | `SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710` |

## Resting set verified (unit)

| Name | Weight | Band | Ledger |
| --- | ---: | --- | --- |
| Dominant | **0.70** | Mid | operator |
| Possessive | 0.52 | Mid | operator-provisional-until-seed |
| Affectionate | 0.52 | Mid | operator-provisional-until-seed |
| Driven | 0.52 | Mid | operator-provisional-until-seed |
| Devoted | 0.52 | Mid | operator-provisional-until-seed |
| Intense | 0.52 | Mid | operator-provisional-until-seed |
| Uninhibited | **0.80** | High | operator |
| Ambitious | **0.80** | High | operator |
| Proud | 0.52 | Mid | operator-provisional-until-seed |

## Commands run

```powershell
dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj -c Release `
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~Prop17LeisureTests" `
  -o .tmp\prop2025-test `
  --verbosity normal
```

Live probes (after soft-wait/retry for Release; OPS `PROP-20.1d-OPS01-to-PM01.md` still absent at probe time):

```powershell
Invoke-WebRequest http://127.0.0.1:7710/health
Invoke-WebRequest http://127.0.0.1:7710/settings/identity
Invoke-WebRequest http://127.0.0.1:7710/journals/books
```

## Test output (pasted)

```
Test run for C:\Users\kurtw\.house_victoria\SoulCore.AI\.tmp\prop2025-test\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.73]   Discovering: SoulCore.Protocol.Tests
[xUnit.net 00:00:00.87]   Discovered:  SoulCore.Protocol.Tests
[xUnit.net 00:00:00.88]   Starting:    SoulCore.Protocol.Tests
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder [172 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_FormatsTitlePlusFloorLine [171 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [172 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureSession_SelfStartsWithoutRelease [1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_WithInjectedFloors_UsesVictoriaMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.CharterBooks_EighteenTitles_ExactlyOneBookEach [2 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights [8 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers [4 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.TruncateFloorLine_CapsLongBodiesWithoutSentence [< 1 ms]
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_PrefersSchemaBookOverTitleMap [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_NeverReusesCloudBaseUrl [59 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ResolveLeisureBaseUrl_HonorsLocalEmbeddingOverride [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_SaysSoOnceUntilWorkMoves [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndNineResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_UnfinishedWork_IsNotASuspendedProposal [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureModel_Default_IsQwen15b [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_RunnableTodoBlocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_WriteSuppressed_WithoutFloorsOrResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureTools_DenyHostRejects [3 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_BuyEmailInstallTrade_Classifies [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_SuspendedDoesNotBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureModel_Empty_MeansQuiet_NeverWorkModelFallback [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ParallelReads_ComposesPreambleWithToolGuidance [72 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.InsertBeforeEmotion_PlacesKnownProceduresAheadOfEmotion [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsMatchingSopIntoPreamble [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ContinuesWhenCharterFails [< 1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.ActionProposal_LandsSuspended_WithoutStartNow [422 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_InProgressLiveSurface_Blocks [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_SmallModelFailure_SelectsDeskChatModel [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.BlockedAside_Excuse_IsNotAResearchFind [12 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.UntrustedObservation_CannotPoisonPreamble [5 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.Eligibility_TodoWithDeadSurface_AllowsRr [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.InferenceLease_Exclusive_AndAbort [< 1 ms]

Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 2.2523 Seconds
```

Key weight assertions exercised:

- `VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights` — Dominant 0.70 Mid operator; Ambitious/Uninhibited 0.80 High operator; six provisional 0.52 Mid
- `VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors` — Contains `Dominant · 0.70 Mid`, `Uninhibited · 0.80 High`, `Ambitious · 0.80 High`
- `ChatContextBuilderTests` preamble Contains same three operator lines

## Live Release smoke (pasted)

Soft-depend timeline: first probes found Debug Host on `:7710` then Host down; waited/retried; Release Host came up on `:7710` (7700 still closed). `docs/agents/reports/PROP-20.1d-OPS01-to-PM01.md` still **absent** at QA close — soft-depend satisfied by live Release process + probes, not by OPS report file.

```
PROBE_AT=2026-11-17 23:28:08
HOST_PID=43112 CMD="C:\Program Files\dotnet\dotnet.exe" ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710
CONFIG=Release

GET /health → 200
status=ok service=SoulCore.Host version=0.1.54 port=7710

GET /settings/identity → 200
displayName=Victoria charter.anchors=18 locked=18 fullyLocked=True mode=locked
identityAnchorsCount=8 books_from_anchors: attributes=3, personality=3, constitution=1, compass=1

GET /journals/books → 200
count=7 ids=feeling,animation,environment,life_hacks,thoughts,wants,dreams
```

## Notes

- Host DLL lock avoided for unit run via `-c Release -o .tmp\prop2025-test`.
- Live bind is **7710**, not default 7700 — probes used the actual Release process URL.
- QA did not modify product code or deploy.

## Handoff

- PM-01: accept PROP-20.25; dispatch **SLOP-01** for operator-weight delta if not already covered.
- OPS-01: still expected to file `PROP-20.1d-OPS01-to-PM01.md` (QA did not block on that file).
