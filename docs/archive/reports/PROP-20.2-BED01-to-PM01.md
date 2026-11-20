---
type: report
prop_id: PROP-20.2
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
---

# PROP-20.2 — Dated recall + today-first memory tape — Pass

## Done

1. **`MemoryRecallLine`** (`Content` + `OccurredAt`) on episodic recall API.
2. **`RecallRecentDatedAsync` / `RecallSimilarDatedAsync`** on `IMemoryStore` + SQLite repo; string recall methods remain as content projections (SMS / tools unchanged).
3. **`MemoryTape`** helper: today-first order, `occurred_at | content` line format, truncation drops older first so today's lines survive a tight budget.
4. **`ChatContextBuilder`**: chat compose uses dated recall → `MemoryTape` / `BuildMemoryBlock`; identity/mind preamble left to PROP-20.4.

## Acceptance

| Criterion | Result |
| --- | --- |
| Recall returns dated content | Pass — dated API returns `OccurredAt` |
| Prompt memory: today first, then older | Pass — `MemoryTape.OrderTodayFirst` + `BuildMemoryBlock` |
| Truncation prefers today | Pass — tight-budget test keeps today, drops older |
| Test evidence pasted | Pass — below |

## Files

- `SoulCore/SoulCore.Memory/MemoryRecallLine.cs` (new)
- `SoulCore/SoulCore.Memory/IMemoryStore.cs`
- `SoulCore/SoulCore.Memory/Repositories/SqliteEpisodicMemoryRepository.cs`
- `SoulCore/SoulCore.Memory/SqliteMemoryStore.cs`
- `SoulCore/SoulCore.Host/Ws/MemoryTape.cs` (new)
- `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` (memory path only)
- `SoulCore/SoulCore.Protocol.Tests/MemoryRecallDatedTests.cs` (new)
- `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` (+ today-first / tight-budget)
- Test stubs implementing `IMemoryStore` updated for dated methods

## Evidence (dotnet test)

Filter: `ChatContextBuilderTests|MemoryRecallDatedTests|SqliteMemoryStoreEmbeddingTests|MemoryToolsTests`

```
Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44, Duration: 1 s - SoulCore.Protocol.Tests.dll (net8.0)
```

PROP-20.2 core cases (detailed):

```
  Passed ChatContextBuilderTests.BuildMemoryBlock_TightBudget_KeepsTodayDropsOlder
  Passed ChatContextBuilderTests.BuildMemoryBlock_TodayFirst_ThenOlderSemanticHits
  Passed ChatContextBuilderTests.BuildMemoryBlock_TruncatesOldestFirst
  Passed MemoryRecallDatedTests.RecallRecentDatedAsync_ReturnsOccurredAtWithContent
  Passed MemoryRecallDatedTests.MemoryTape_OrderTodayFirst_TwoDatedEpisodes
  Passed MemoryRecallDatedTests.RecallSimilarDatedAsync_ReturnsOccurredAtWithContent
Test Run Successful.
Total tests: 6
     Passed: 6
```

## Coordination notes

- Did not edit SMS Companion, LeisureRunner, or journal books.
- `ChatContextBuilder` mind/identity path owned by PROP-20.4; memory helpers live in `MemoryTape`.
