---
type: report
prop_id: PROP-18.18
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.12-BED01-to-PM01.md
task: docs/agents/tasks/PROP-18.18-PM01-to-QA01.md
---

# PROP-18.18 — QA SMS MemoryTape + steering — Pass

## Result

**Pass.** SMS preamble uses shared `MemoryTape.BuildMemoryBlock` (bullet `linePrefix: "- "`, `maxLines: SmsMemoryRecallLimit`) then `MemorySteeringGuidance.AppendToPreamble`. Desk and SMS both keep today-first dated `[Memory]` lines; SMS gets `[How I use memory]` / “do not invent a clock” after `[Memory]`.

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| F1 — SMS gets MemorySteeringGuidance after `[Memory]` | **Pass** | `BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt` asserts `MemorySteeringGuidance.Marker` + `"do not invent a clock"`; source: `SmsInboundService` → `AppendToPreamble(memoryBlock)` |
| F2 — One assembly path via `MemoryTape.BuildMemoryBlock` (SMS `- ` prefix) | **Pass** | `SmsInboundService` calls `BuildMemoryBlock(..., linePrefix: "- ", maxLines: SmsMemoryRecallLimit)` only — no local OrderTodayFirst/FormatLine loop; `MemoryTape_BuildMemoryBlock_LinePrefixAndMaxLines` |
| Desk + SMS dated today-first | **Pass** | Desk: `BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits`, `BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder`, `MemoryTape_OrderTodayFirst_TwoDatedEpisodes`; SMS: today coffee before older tea in `BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt` |

## Source spot-check

| Location | Behavior |
| --- | --- |
| `SoulCore.Host/Companion/SmsInboundService.cs` | PROP-18.12 path: `BuildMemoryBlock` + `AppendToPreamble`; `SmsMemoryRecallLimit = 6` |
| `SoulCore.Host/Ws/MemoryTape.cs` | Optional `linePrefix` / `maxLines`; today-first; extras drop from end |
| `SoulCore.Core/MemorySteeringGuidance.cs` | Marker `[How I use memory]`; “do not invent a clock” |
| `SoulCore.Host/Ws/ChatContextBuilder.cs` | Desk still `MemoryTape.BuildMemoryBlock` + `AppendToPreamble` |

## Evidence (`dotnet test`)

Filter (BED-aligned):

```text
FullyQualifiedName~SmsInboundServiceTests
|FullyQualifiedName~MemoryRecallDatedTests
|FullyQualifiedName~ChatContextBuilderTests
|FullyQualifiedName~MemorySteeringGuidanceTests
```

Command:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~SmsInboundServiceTests|FullyQualifiedName~MemoryRecallDatedTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~MemorySteeringGuidanceTests" --verbosity normal
```

### Combined gate (32 tests)

```text
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder [3 ms]
Passed SoulCore.Protocol.Tests.MemorySteeringGuidanceTests.MatchProcedures_RanksMaestroSopForClassAsk [4 ms]
Passed SoulCore.Protocol.Tests.MemorySteeringGuidanceTests.MatchProcedures_ReturnsEmptyWhenNoOverlap [1 ms]
Passed SoulCore.Protocol.Tests.MemorySteeringGuidanceTests.BuildKnownProceduresBlock_InstructsSopGet [< 1 ms]
Passed SoulCore.Protocol.Tests.MemorySteeringGuidanceTests.AppendToPreamble_IsIdempotent [< 1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_SmsAllowListOnly_NoDeniedToolAgency [10 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_WithoutToolLoop_ForbidsTools [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ParallelReads_ComposesPreambleWithToolGuidance [57 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.InsertBeforeEmotion_PlacesKnownProceduresAheadOfEmotion [1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_StaleContact_OmitsContextTraits_KeepsResting [3 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits [1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_UnrelatedTurn_OmitsCompass_KeepsFloorsAndResting [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_FreshTopicalTurn_InjectsAtMostThreeContextTraits [2 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsMatchingSopIntoPreamble [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_ContinuesWhenCharterFails [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_MatchingTurn_InjectsAtMostThreeCompassLines [1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.Allowlisted_WithoutRegistry_UsesCompleteAsync_AppendsPresenceHistory [78 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.InboundImage_StoredAsMedia_NotToolInput [< 1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt [< 1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_LocksKayleighAddressEvenIfMemorySilent [< 1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.ModelDown_StubEnabled_ReturnsStubOk [< 1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.UnknownSender_SilentDrop_NoInference [< 1 ms]
Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.ModelDown_StubDisabled_ReturnsChatModelDown [< 1 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.RecallRecentDatedAsync_ReturnsOccurredAtWithContent [405 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_OrderTodayFirst_TwoDatedEpisodes [< 1 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.RecallSimilarDatedAsync_ReturnsOccurredAtWithContent [330 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_BuildMemoryBlock_LinePrefixAndMaxLines [< 1 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_FormatLine_ExposesOccurredAtForChronology [< 1 ms]

Test Run Successful.
Total tests: 32
     Passed: 32
 Total time: 1.4912 Seconds
```

## Issues

None new for PROP-18.12 / PROP-18.18 SMS memory hygiene.

## Handoff

PM-01 may accept PROP-18.18 / PROP-18.12 SMS MemoryTape + steering. Dispatch **SLOP-01** for post-QA slop audit.
