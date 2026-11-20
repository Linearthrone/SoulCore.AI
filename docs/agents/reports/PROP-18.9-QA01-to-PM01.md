---
type: report
prop_id: PROP-18.9
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Completed
created: 2026-11-19
depends_on: PROP-18.1
verdict: Pass
---

# PROP-18.9 — QA dated memory — Pass

## Verdict: **Pass**

Verified today-first dated `[Memory]` on desk (ChatContextBuilder / MemoryTape) and SMS (`SmsInboundService.BuildSmsPreamble`) paths. Store `occurred_at` stamps appear in compose lines; steering cue forbids inventing a clock. Ready for SLOP-01.

## Scope

| Item | Source |
| --- | --- |
| Task | `docs/agents/tasks/PROP-18.9-PM01-to-QA01.md` |
| Upstream | `docs/agents/reports/PROP-18.1-BED01-to-PM01.md` (Pass) |
| Role | `@Agents/QA-01.md` |

## Acceptance

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Desk `[Memory]` shows `occurred_at \| content` | **Pass** | `MemoryTape.FormatLine` + `BuildMemoryBlock` dump; `ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_*` |
| 2 | SMS `[Memory]` shows dated lines, today-first | **Pass** | Live SMS preamble dump; `SmsInboundServiceTests.BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt` |
| 3 | Today-first then older; two episodes orderable by stamp | **Pass** | Desk + SMS dumps (today coffee before older tea); `MemoryTape_OrderTodayFirst_TwoDatedEpisodes` |
| 4 | No invented clock — store stamps steer chronology | **Pass** | Steering dump + `MemorySteeringGuidanceTests.AppendToPreamble_IsIdempotent` asserts `do not invent a clock` |
| 5 | Similarity dated recall kept (chat path) | **Pass** | `RecallSimilarDatedAsync_ReturnsOccurredAtWithContent` |
| 6 | Tests with real output | **Pass** | Filter 12/12 below + compose dump |

## Commands run

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~MemoryRecallDatedTests|FullyQualifiedName~SmsInboundServiceTests.BuildSmsPreamble|FullyQualifiedName~ChatContextBuilderTests.BuildMemoryBlock|FullyQualifiedName~MemorySteeringGuidanceTests.AppendToPreamble" `
  -v n

# Live compose sample (tmpcode harness, Debug — QA only, not product)
dotnet run --project tmpcode/prop189-dump/prop189-dump.csproj -c Debug
```

## Test output (real)

```
  Passed SoulCore.Protocol.Tests.MemorySteeringGuidanceTests.AppendToPreamble_IsIdempotent [2 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder [3 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits [1 ms]
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_SmsAllowListOnly_NoDeniedToolAgency [10 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_WithoutToolLoop_ForbidsTools [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundServiceTests.BuildSmsPreamble_LocksKayleighAddressEvenIfMemorySilent [< 1 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.RecallRecentDatedAsync_ReturnsOccurredAtWithContent [352 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_OrderTodayFirst_TwoDatedEpisodes [< 1 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.RecallSimilarDatedAsync_ReturnsOccurredAtWithContent [355 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_FormatLine_ExposesOccurredAtForChronology [< 1 ms]

Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12
```

## Live compose dump (utcNow = 2026-11-19)

```
=== MemoryTape.FormatLine ===
2026-11-19T08:00:00.000Z | today episode coffee
2026-11-10T09:00:00.000Z | older episode tea
=== Desk BuildMemoryBlock today-first ===
dropped=0
[Memory]
2026-11-19T08:00:00.000Z | today episode coffee
2026-11-10T09:00:00.000Z | older episode tea

=== SMS BuildSmsPreamble [Memory] excerpt ===
FOUND [Memory]
[Memory]
- 2026-11-19T08:00:00.000Z | today episode coffee
- 2026-11-10T09:00:00.000Z | older episode tea
PASS SMS today-first order
=== Steering cue ===
PASS cue: do not invent a clock
PASS cue: order days from store stamp
```

## Notes

- No new issues filed.
- QA did not modify product code.
- Host Release rebuild currently fails on unrelated `IdleParkedBudgetDto` symbols; Debug Host + Protocol.Tests filter used for this gate (same as BED-18.1 evidence path).

## Next

PM-01: dispatch **SLOP-01** post-QA audit for PROP-18.1 / PROP-18.9 chain.
