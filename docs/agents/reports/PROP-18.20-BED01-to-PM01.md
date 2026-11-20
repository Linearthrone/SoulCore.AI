---
type: report
prop_id: PROP-18.20
prop_root: PROP-18-chronology-and-felt-persistence
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
depends_on: PROP-18.17
related_slop: docs/agents/reports/PROP-18.17-SLOP01-to-PM01.md
related_accept: docs/agents/reports/PROP-18.17-PM01-accept.md
---

# PROP-18.20 — Daybook/tool chronology dedupe — Pass

## Verdict: **Pass**

F1–F3 from PROP-18.17 SLOP are closed. One shared helper in `SoulCore.Memory` now owns journal/activity provenance text, occurred-at parse, and truncate-with-limit. Desk (`DaybookTape`) and tool (`RecallTimelineTool`) both call it — activity empty fields no longer diverge into `" /  — "` junk.

## Done

| Finding | Fix |
| --- | --- |
| **F1** | `ChronologyText.FormatJournalProvenance` + `FormatActivityText` — `DaybookTape.FromJournal` / `FromActivity` and `RecallTimelineTool` share the same mappers; empty activity/subject/desc skipped consistently. |
| **F2** | `ChronologyText.TryParseOccurredAt` — `DaybookTape.OrderForInject`, `RecallTimelineTool` window/sort, and `MemoryTape.TryParseOccurredAt` all delegate here (no twin parser). |
| **F3** | `ChronologyText.TruncateBody(body, charLimit)` — desk uses 280, tool uses 400; one whitespace-normalize path (CRLF + double-space collapse). |

## Files

- `SoulCore/SoulCore.Memory/ChronologyText.cs` (new)
- `SoulCore/SoulCore.Host/Ws/DaybookTape.cs`
- `SoulCore/SoulCore.Host/Ws/MemoryTape.cs`
- `SoulCore/SoulCore.Inference/Tools/RecallTimelineTool.cs`
- `SoulCore/SoulCore.Protocol.Tests/DaybookTapeTests.cs` (+ empty-field + truncate/limit + parse-delegate coverage)

## Acceptance

| Criterion | Result |
| --- | --- |
| One provenance formatter for DaybookTape + RecallTimelineTool | Pass |
| Activity empty-field text matches | Pass |
| One occurred-at parse reused by tape + tool | Pass |
| One truncate helper with limit params (280 vs 400) | Pass |
| Tests green | Pass — below |

## Evidence (`dotnet test`)

Filter: `FullyQualifiedName~DaybookTapeTests|FullyQualifiedName~ChronologyText|FullyQualifiedName~MemoryTape`

```
Passed!  - Failed:     0, Passed:    12, Skipped:     0, Total:    12, Duration: 772 ms - SoulCore.Protocol.Tests.dll (net8.0)
```

Sample shared activity path (empty subject/desc → no junk separators):

```
activity-log provenance + text "research"  (not "research /  — ")
```

## Next

PM: archive PROP-18.20 when accepted; daybook surface (PROP-18.2 → 18.13 → 18.17 → 18.20) ready for “done” if no further SLOP re-ticket.
