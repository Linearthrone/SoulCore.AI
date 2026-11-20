---
type: report
from: BED-01
to: PM-01
task_id: PROP-18.12
prop_id: PROP-18.12
prop_root: PROP-18-chronology-and-felt-persistence
status: Pass
created: 2026-11-19
depends_on: PROP-18.10
---

# PROP-18.12 — BED-01 → PM-01

## Verdict

**Pass**

## Scope

- F1: SMS preamble calls `MemorySteeringGuidance.AppendToPreamble` after dated `[Memory]` (same cue as desk).
- F2: SMS stops hand-rolling tape lines; uses shared `MemoryTape.BuildMemoryBlock` with optional SMS bullet prefix / maxLines.

## Changes

| File | Change |
| --- | --- |
| `SoulCore/SoulCore.Host/Ws/MemoryTape.cs` | `BuildMemoryBlock` gains optional `linePrefix` and `maxLines` (today-first; extras drop from end). |
| `SoulCore/SoulCore.Host/Companion/SmsInboundService.cs` | Replaces OrderTodayFirst/FormatLine/`- ` loop with `MemoryTape.BuildMemoryBlock(..., linePrefix: "- ", maxLines: SmsMemoryRecallLimit)` then `MemorySteeringGuidance.AppendToPreamble`. |
| `SoulCore/SoulCore.Protocol.Tests/SmsInboundServiceTests.cs` | Asserts SMS bullet lines + steering Marker / "do not invent a clock". |
| `SoulCore/SoulCore.Protocol.Tests/MemoryRecallDatedTests.cs` | Asserts `linePrefix` + `maxLines` on shared builder. |

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~SmsInboundServiceTests|FullyQualifiedName~MemoryRecallDatedTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~MemorySteeringGuidanceTests"

Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32, Duration: 719 ms
```

## Acceptance checklist

| Item | Result |
| --- | --- |
| F1 — SMS gets `[How I use memory]` / do-not-invent-a-clock after `[Memory]` | Pass |
| F2 — One assembly path via `MemoryTape.BuildMemoryBlock` (SMS `- ` prefix) | Pass |
| Desk `ChatContextBuilder` / tape tests still green | Pass (included in filter) |
