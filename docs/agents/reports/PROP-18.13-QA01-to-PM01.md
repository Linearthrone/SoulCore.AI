---
type: report
prop_id: PROP-18.13
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.2-BED01-to-PM01.md
task: docs/agents/tasks/PROP-18.13-PM01-to-QA01.md
---

# PROP-18.13 — QA daybook inject — Pass

## Result

**Pass.** Desk chat injects a provenance-tagged `[Daybook]` block (journals + optional activity-log crumbs) ahead of `[Memory]` inside the identity/memory budget. Leisure soft caps hold (line count, body truncate, char budget, tight-budget drop). Optional `recall_timeline` returns provenance-tagged journal / activity-log / episodic rows and is registered in memory DI.

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| `[Daybook]` provenance tags (`provenance \| occurred_at \| text`) | **Pass** | `FormatLine_ExposesProvenanceAndOccurredAt`; leisure → `journal:{book}/leisure`; activity → `activity-log` |
| Inject order: mind → `[Daybook]` → `[Memory]` | **Pass** | `BuildContextPreamble_InjectsDaybookBeforeMemoryInsideBudget`; live SQLite `BuildAsync_InjectsDaybookFromJournalAndActivityLog` |
| Soft budget (≤2400; ≤⅓ remaining; ≤5 journals / ≤3 crumbs; body ≤280) | **Pass** | Constants in `DaybookTape`; `ChatContextBuilder` `daybookBudget = Min(2400, remaining/3)`; budget tests Pass |
| Leisure soft caps (does not flood) | **Pass** | `BuildDaybookBlock_TightBudget_DropsLeisureTailKeepsHeaderProvenance` — drops ≥1 leisure lines, keeps header + provenance, `block.Length ≤ 700` |
| Today-preferred ordering + activity crumbs | **Pass** | `BuildDaybookBlock_TodayPreferredFirst_ActivityCrumbsTagged` |
| `recall_timeline` present + tagged rows | **Pass** | `RecallTimelineTool_ReturnsProvenanceTaggedRows`; DI `AddSingleton<ITool, RecallTimelineTool>()` |

## Soft-cap constants (source spot-check)

| Knob | Value |
| --- | --- |
| `DaybookTape.DaybookCharBudget` | 2400 |
| `DaybookTape.MaxJournalLines` | 5 |
| `DaybookTape.MaxActivityCrumbs` | 3 |
| `DaybookTape.BodyCharLimit` | 280 |
| Preamble share | `Min(DaybookCharBudget, remaining / 3)` |

## Evidence (`dotnet test`)

Filter (BED-aligned + full DaybookTape suite):

```text
FullyQualifiedName~DaybookTapeTests
|FullyQualifiedName~ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion
|FullyQualifiedName~ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock
```

### Combined gate (9 tests)

```text
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_BudgetHoldsWithMemoryAndMindBlock [7 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildContextPreamble_InjectsDaybookBeforeMemoryInsideBudget [7 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FromJournal_TagsLeisureSourceInProvenance [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FormatLine_ExposesProvenanceAndOccurredAt [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildDaybookBlock_TightBudget_DropsLeisureTailKeepsHeaderProvenance [1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildAsync_InjectsDaybookFromJournalAndActivityLog [364 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.RecallTimelineTool_ReturnsProvenanceTaggedRows [310 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildDaybookBlock_TodayPreferredFirst_ActivityCrumbsTagged [1 ms]

Test Run Successful.
Total tests: 9
     Passed: 9
 Total time: 1.2306 Seconds
```

### DaybookTape-only re-run (7 tests, TRX)

```text
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildContextPreamble_InjectsDaybookBeforeMemoryInsideBudget [8 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FromJournal_TagsLeisureSourceInProvenance [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FormatLine_ExposesProvenanceAndOccurredAt [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildDaybookBlock_TightBudget_DropsLeisureTailKeepsHeaderProvenance [1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildAsync_InjectsDaybookFromJournalAndActivityLog [381 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.RecallTimelineTool_ReturnsProvenanceTaggedRows [356 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildDaybookBlock_TodayPreferredFirst_ActivityCrumbsTagged [1 ms]

Test Run Successful.
Total tests: 7
     Passed: 7
Results File: SoulCore\SoulCore.Protocol.Tests\TestResults\prop1813-daybook.trx
```

Sample tagged lines (from BED-01 / asserted by suite):

```text
journal:feeling | 2026-11-19T08:00:00.000Z | felt curious about chronology
activity-log | 2026-11-19T09:15:00.000Z | research / PROP-18.2 — Sketched daybook provenance tags so leisure notes stay short.
```

Preamble order verified: `[Identity]` → mind → `[Daybook]` → `[Memory]` → steering → `[SoulCore emotion]`.

## Integration wiring (spot-check)

| Location | Behavior |
| --- | --- |
| `SoulCore.Host/Ws/DaybookTape.cs` | Format, order, soft caps, `BuildDaybookBlock` |
| `SoulCore.Host/Ws/ChatContextBuilder.cs` | Parallel journal + activity load; daybook ≤⅓ remaining; inject before `[Memory]` |
| `SoulCore.Inference/Tools/RecallTimelineTool.cs` | Tool name `recall_timeline`; windows recent/today/week |
| `MemoryServiceCollectionExtensions.cs` | `services.AddSingleton<ITool, RecallTimelineTool>()` |

## Issues

None new for PROP-18.2 / PROP-18.13 daybook inject.

## Handoff

PM-01 may accept PROP-18.13 / PROP-18.2 daybook bridge. Dispatch **SLOP-01** for post-QA slop audit.
