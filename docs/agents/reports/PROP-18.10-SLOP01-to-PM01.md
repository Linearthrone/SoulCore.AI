---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.10
prop_id: PROP-18.10
prop_root: PROP-18-chronology-and-felt-persistence
status: findings
created: 2026-11-19
related_qa: docs/agents/reports/PROP-18.9-QA01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.1-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.10 (PROP-18.1 dated recall surface)

## Scope

- Related QA: `docs/agents/reports/PROP-18.9-QA01-to-PM01.md` (Pass)
- Related BED: `docs/agents/reports/PROP-18.1-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore/SoulCore.Host/Ws/MemoryTape.cs`
  - `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` (dated recall + `BuildMemoryBlock` façades + steering append)
  - `SoulCore/SoulCore.Host/Companion/SmsInboundService.cs` (dated recall + `BuildSmsPreamble`)
  - `SoulCore/SoulCore.Core/MemorySteeringGuidance.cs`
  - `SoulCore/SoulCore.Memory/IMemoryStore.cs`, `SqliteEpisodicMemoryRepository.cs` (dated vs undated recall)
  - `SoulCore/SoulCore.Inference/Tools/RecallTimelineTool.cs` (adjacent dated consumer)
  - Tests: `MemoryRecallDatedTests`, `SmsInboundServiceTests.BuildSmsPreamble_*`, `ChatContextBuilderTests.BuildMemoryBlock_*`, `MemorySteeringGuidanceTests`
- Method: read primary sources + ripgrep for `OrderTodayFirst` / `FormatLine` / `Recall*Dated` / `invent a clock` / `MemorySteeringGuidance` / `BuildMemoryBlock`
- **Read-only.** No code modified.

## Summary

- Findings count: **2**
- Highest severity: **P1**
- Verdict: **findings**

## Findings

### F1 — SMS preamble omits shared chronology steering

- Category: duplicate (divergent channel path)
- Severity: **P1**
- Evidence:
  - Desk: `ChatContextBuilder.BuildContextPreamble` → `MemorySteeringGuidance.AppendToPreamble(memoryBlock)` after dated `[Memory]` (`ChatContextBuilder.cs` ~214).
  - SMS: `SmsInboundService.BuildSmsPreamble(MemoryRecallLine…)` builds `[Memory]` via `MemoryTape` helpers but **never** calls `MemorySteeringGuidance.AppendToPreamble` (rg under `Companion/` → 0 matches).
  - Single cue body lives only in `MemorySteeringGuidance.Block` (`do not invent a clock`) — no second string alias, but SMS never receives it.
- Why it matters: PROP-18.1 acceptance requires the model to order days from store stamps without inventing a clock. Desk and SMS both surface dated lines, but only desk gets the shared steering contract → divergent chronology behavior across channels.
- Recommended action: **dedupe**
- Suggested owner: **BED-01**
- Ask-user question: (n/a)

### F2 — SMS hand-assembles `[Memory]` instead of `MemoryTape.BuildMemoryBlock`

- Category: duplicate
- Severity: **P2**
- Evidence:
  - Desk dated path: `ChatContextBuilder.BuildMemoryBlock` → `MemoryTape.BuildMemoryBlock` (order + format + budget truncate).
  - SMS: `BuildSmsPreamble` re-opens `OrderTodayFirst` + `FormatLine` + manual `[Memory]\n` / `- ` bullet loop (`SmsInboundService.cs` ~542–555), capped by `SmsMemoryRecallLimit` only (no char-budget twin).
- Why it matters: same tape purpose under two assembly shapes; future format/order changes can drift between desk and SMS.
- Recommended action: **ask-user**
- Suggested owner if remove/dedupe: **BED-01**
- Ask-user question: Keep SMS bullet (`- occurred_at | content`) as intentional SMS list style, or fold into `MemoryTape` with an optional line prefix / shared line enumerator so only one assembly path remains?

## Clean areas

- **No clock-invention aliases:** one `MemorySteeringGuidance.Block` string; no parallel “don’t invent a clock” helpers.
- **Single today-first tape API:** only `MemoryTape.OrderTodayFirst` / `FormatLine` for `MemoryRecallLine`.
- **Single dated store APIs:** `RecallRecentDatedAsync` / `RecallSimilarDatedAsync`; undated `RecallRecentAsync` / `RecallSimilarAsync` correctly project content from dated (compat, not a second chronology invent).
- **Desk façades intentional:** `ChatContextBuilder.BuildMemoryBlock` overloads remain thin delegates to `MemoryTape` (PROP-20.13 F8 residual — not re-flagged).
- **Daybook adjacent reuse:** `DaybookTape` calls `MemoryTape.TryParseOccurredAt` (good); provenance today-first buckets are PROP-18.2, out of this ticket’s cleanup ask.
- **PROP ticket comments** on SMS dated path are local convention, not noise worth a finding.

## Notes for PM

- Safe to archive? **no** — prefer BED cleanup (at least **F1**) before user-facing “done” on the PROP-18.1 → 18.9 → 18.10 chain.
- **F1** is the blocking hygiene item (wire `AppendToPreamble` on SMS after `[Memory]`, same as desk).
- **F2** is optional / ask-user; defer without blocking if LinearThrone wants SMS bullets kept.
- After BED cleanup: normal OPS → QA as PM prefers; re-ticket SLOP only if the cleanup grows beyond the SMS preamble wire.
