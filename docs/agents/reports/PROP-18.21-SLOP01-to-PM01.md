---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.21
prop_id: PROP-18.21
prop_root: PROP-18-chronology-and-felt-persistence
status: clean
created: 2026-11-19
related_qa: docs/agents/reports/PROP-18.18-QA01-to-PM01.md
related_slop: docs/agents/reports/PROP-18.10-SLOP01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.12-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.21 (PROP-18.12 F1/F2 re-check)

## Scope

- Related QA: `docs/agents/reports/PROP-18.18-QA01-to-PM01.md` (Pass)
- Related prior SLOP: `docs/agents/reports/PROP-18.10-SLOP01-to-PM01.md` (findings F1/F2)
- Related BED cleanup: `docs/agents/reports/PROP-18.12-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore/SoulCore.Host/Companion/SmsInboundService.cs` (`BuildSmsPreamble`)
  - `SoulCore/SoulCore.Host/Ws/MemoryTape.cs` (`BuildMemoryBlock` + `linePrefix` / `maxLines`)
  - `SoulCore/SoulCore.Core/MemorySteeringGuidance.cs` (via test + call site)
  - Tests: `SmsInboundServiceTests.BuildSmsPreamble_DatedLines_*`, `MemoryRecallDatedTests.MemoryTape_BuildMemoryBlock_LinePrefixAndMaxLines`
- Method: read primary sources + ripgrep (`MemorySteeringGuidance` / `BuildMemoryBlock` / `OrderTodayFirst` / `FormatLine` under Companion)
- **Read-only.** No code modified.

## Summary

- Findings count: **0**
- Highest severity: (n/a)
- Verdict: **clean**
- PROP-18.10 **F1** and **F2** are **closed** by PROP-18.12.

## Closure matrix

| Prior finding | Status | Evidence |
| --- | --- | --- |
| **F1** — SMS omits `MemorySteeringGuidance.AppendToPreamble` | **Closed** | `SmsInboundService.BuildSmsPreamble` appends via `MemorySteeringGuidance.AppendToPreamble(memoryBlock)` after dated `[Memory]` (~553). Test asserts `MemorySteeringGuidance.Marker` + `"do not invent a clock"`. |
| **F2** — SMS hand-assembles `[Memory]` instead of `MemoryTape.BuildMemoryBlock` | **Closed** | SMS calls `MemoryTape.BuildMemoryBlock(..., linePrefix: "- ", maxLines: SmsMemoryRecallLimit)` (~546–551). rg under `SmsInboundService.cs` → **0** matches for `OrderTodayFirst` / `FormatLine`. Shared builder owns order + format + optional prefix/cap. |

## Clean areas

- **One assembly path:** desk and SMS both route dated lines through `MemoryTape.BuildMemoryBlock`; SMS differs only by intentional `linePrefix` / `maxLines`.
- **One chronology cue:** SMS now receives the same `MemorySteeringGuidance` block as desk after `[Memory]`.
- **No residual SMS tape loop:** Companion no longer re-opens `OrderTodayFirst` / `FormatLine` / manual `- ` bullets.
- PROP-18.12 ticket comment on the SMS path is local convention, not noise.

## Notes for PM

- Safe to archive? **yes** — F1/F2 hygiene items from PROP-18.10 are verified closed; no new actionable slop in this delta.
- No DEV re-ticket needed from this re-check.
