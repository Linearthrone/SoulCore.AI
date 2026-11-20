---
type: report
prop_id: PROP-18.1
prop_root: PROP-18-chronology-and-felt-persistence
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
---

# PROP-18.1 — Dated memory compose — Pass

## Verdict: **Pass**

Chat already had PROP-20.2 dated recall + today-first `MemoryTape`. PROP-18.1 closes the SMS gap so both channels surface `occurred_at` in `[Memory]`, keeps similarity search on desk chat, and steers the model to order by store stamps (no invented clock).

## Done

1. **SMS dated path** — `SmsInboundService` uses `RecallRecentDatedAsync` (`SmsMemoryRecallLimit = 6`) and `BuildSmsPreamble(MemoryRecallLine…)` → `MemoryTape.OrderTodayFirst` + `FormatLine` (`occurred_at | content`).
2. **Desk chat unchanged contract** — `ChatContextBuilder` still: similarity dated → recent dated fallback → today-first tape; string overload remains undated-compat only.
3. **Chronology cue** — `MemorySteeringGuidance.Block` tells the model store stamps order days; do not invent a clock.
4. **Tests** — real output for dated format, today-first order (chat + SMS), similarity dated recall.

## Out of scope (honored)

Activities log UI, idle agency budget, voice wake; PROP-18.2 daybook.

## Acceptance

| Criterion | Result |
| --- | --- |
| Chat/SMS `[Memory]` shows `occurred_at` | Pass |
| Today-first, then older; similarity kept on chat | Pass |
| Two episodes orderable by day from stamps | Pass |
| Tests with real output | Pass — below |

## Files

- `SoulCore/SoulCore.Host/Companion/SmsInboundService.cs`
- `SoulCore/SoulCore.Core/MemorySteeringGuidance.cs`
- `SoulCore/SoulCore.Protocol.Tests/MemoryRecallDatedTests.cs`
- `SoulCore/SoulCore.Protocol.Tests/SmsInboundServiceTests.cs`
- `SoulCore/SoulCore.Protocol.Tests/MemorySteeringGuidanceTests.cs`

## Evidence (`dotnet test`)

Filter: `MemoryRecallDatedTests|SmsInboundServiceTests.BuildSmsPreamble|ChatContextBuilderTests.BuildMemoryBlock|MemorySteeringGuidanceTests.AppendToPreamble`

```
  Passed MemorySteeringGuidanceTests.AppendToPreamble_IsIdempotent
  Passed ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder
  Passed ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits
  Passed ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst
  Passed SmsInboundServiceTests.BuildSmsPreamble_SmsAllowListOnly_NoDeniedToolAgency
  Passed SmsInboundServiceTests.BuildSmsPreamble_WithoutToolLoop_ForbidsTools
  Passed SmsInboundServiceTests.BuildSmsPreamble_DatedLines_TodayFirstWithOccurredAt
  Passed SmsInboundServiceTests.BuildSmsPreamble_LocksKayleighAddressEvenIfMemorySilent
  Passed MemoryRecallDatedTests.RecallRecentDatedAsync_ReturnsOccurredAtWithContent
  Passed MemoryRecallDatedTests.MemoryTape_OrderTodayFirst_TwoDatedEpisodes
  Passed MemoryRecallDatedTests.RecallSimilarDatedAsync_ReturnsOccurredAtWithContent
  Passed MemoryRecallDatedTests.MemoryTape_FormatLine_ExposesOccurredAtForChronology
Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12
```

Sample compose line (from `MemoryTape_FormatLine_ExposesOccurredAtForChronology`):

```
2026-11-19T08:00:00.000Z | coffee plan
2026-11-10T09:00:00.000Z | tea with Kayleigh
```

## Next

PROP-18.2 (daybook bridge) when PM dispatches.
