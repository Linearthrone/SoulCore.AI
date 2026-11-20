---
type: report
prop_id: PROP-18.22
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.20-BED01-to-PM01.md
task: docs/agents/tasks/PROP-18.22-PM01-to-QA01.md
---

# PROP-18.22 — QA ChronologyText / daybook provenance dedupe — Pass

## Result

**Pass.** DaybookTape and RecallTimelineTool both call `SoulCore.Memory.ChronologyText` for journal provenance, activity-log text (empty fields skipped), occurred-at parse, and truncate-with-limit (desk 280 / tool 400). Empty activity subject/desc does not emit `" / "` or `" — "` junk. MemoryTape parse delegates to the same helper.

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| One provenance formatter shared by DaybookTape + RecallTimelineTool | **Pass** | Both call `ChronologyText.FormatJournalProvenance` / `FormatActivityText` / `ActivityLogProvenance`; `FromJournal_TagsLeisureSourceInProvenance`, `RecallTimelineTool_ReturnsProvenanceTaggedRows` |
| Activity empty-field text matches (no `" /  — "` junk) | **Pass** | `FromActivity_EmptyFields_MatchChronologyText` — text `"research"`, equals shared `FormatActivityText`, no `" / "` / `" — "` |
| One occurred-at parse reused by tape + tool | **Pass** | `DaybookTape.OrderForInject`, `RecallTimelineTool` window/sort, `MemoryTape.TryParseOccurredAt` → `ChronologyText.TryParseOccurredAt`; `ChronologyText_Truncate_RespectsLimitParams` asserts MemoryTape delegate equality |
| Truncate helper with limit params (280 vs 400) | **Pass** | `DaybookTape.BodyCharLimit=280`, `RecallTimelineTool.BodyCharLimit=400`; `ChronologyText_Truncate_RespectsLimitParams` |
| Tests green | **Pass** | 12/12 below |

## Source spot-check (read-only)

| Location | Behavior |
| --- | --- |
| `SoulCore.Memory/ChronologyText.cs` | Shared `FormatJournalProvenance`, `FormatActivityText`, `TryParseOccurredAt`, `TruncateBody` |
| `SoulCore.Host/Ws/DaybookTape.cs` | `FromJournal` / `FromActivity` / `OrderForInject` / `TruncateBody` → ChronologyText; `BodyCharLimit = 280` |
| `SoulCore.Inference/Tools/RecallTimelineTool.cs` | Journal/activity/episodic rows + window/sort → ChronologyText; `BodyCharLimit = 400` |
| `SoulCore.Host/Ws/MemoryTape.cs` | `TryParseOccurredAt` delegates to ChronologyText |

## Evidence (`dotnet test`)

Filter (BED-aligned):

```text
FullyQualifiedName~DaybookTapeTests|FullyQualifiedName~ChronologyText|FullyQualifiedName~MemoryTape
```

Command:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~DaybookTapeTests|FullyQualifiedName~ChronologyText|FullyQualifiedName~MemoryTape" --verbosity normal
```

### Combined gate (12 tests)

```text
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_OrderTodayFirst_TwoDatedEpisodes [4 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildContextPreamble_InjectsDaybookBeforeMemoryInsideBudget [6 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_BuildMemoryBlock_LinePrefixAndMaxLines [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FromJournal_TagsLeisureSourceInProvenance [< 1 ms]
Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_FormatLine_ExposesOccurredAtForChronology [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.ChronologyText_Truncate_RespectsLimitParams [1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FormatLine_ExposesProvenanceAndOccurredAt [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildDaybookBlock_TightBudget_DropsLeisureTailKeepsHeaderProvenance [1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildAsync_InjectsDaybookFromJournalAndActivityLog [386 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.RecallTimelineTool_ReturnsProvenanceTaggedRows [350 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.BuildDaybookBlock_TodayPreferredFirst_ActivityCrumbsTagged [< 1 ms]
Passed SoulCore.Protocol.Tests.DaybookTapeTests.FromActivity_EmptyFields_MatchChronologyText [< 1 ms]
Test Run Successful.
Total tests: 12
```

Summary line (same filter, earlier run with build):

```text
Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 730 ms - SoulCore.Protocol.Tests.dll (net8.0)
```

Sample empty-field path (asserted by `FromActivity_EmptyFields_MatchChronologyText`):

```text
activity-log provenance + text "research"  (not "research /  — ")
```

## Issues

None.

## Next

PM: archive PROP-18.22 when accepted; dispatch **SLOP-01** for post-QA slop audit on the ChronologyText share path (PROP-18.20 surface).
