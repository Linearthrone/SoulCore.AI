---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.26
prop_id: PROP-18.26
prop_root: PROP-18-chronology-and-felt-persistence
status: clean
created: 2026-11-19
related_slop: docs/agents/reports/PROP-18.17-SLOP01-to-PM01.md
related_cleanup: docs/agents/reports/PROP-18.20-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.26 (ChronologyText F1–F3 re-check)

## Scope

- Related prior SLOP: `docs/agents/reports/PROP-18.17-SLOP01-to-PM01.md` (findings F1–F3)
- Related BED cleanup: `docs/agents/reports/PROP-18.20-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore/SoulCore.Memory/ChronologyText.cs` (shared helper)
  - `SoulCore/SoulCore.Host/Ws/DaybookTape.cs`
  - `SoulCore/SoulCore.Host/Ws/MemoryTape.cs` (`TryParseOccurredAt` delegate)
  - `SoulCore/SoulCore.Inference/Tools/RecallTimelineTool.cs`
  - Tests: `SoulCore/SoulCore.Protocol.Tests/DaybookTapeTests.cs` (shared-path coverage)
- Method: read primary sources + ripgrep for `ChronologyText.*` / leftover private parse-truncate / inline `journal:` provenance
- **Read-only.** No code modified.

## Summary

- Findings count: **0**
- Highest severity: (n/a)
- Verdict: **clean** — PROP-18.17 F1–F3 confirmed closed

## F1–F3 closure check

| Finding | Status | Evidence |
| --- | --- | --- |
| **F1** journal/activity provenance + activity empty-field text | **closed** | Desk + tool both call `ChronologyText.FormatJournalProvenance` / `FormatActivityText` (`DaybookTape.FromJournal` / `FromActivity`; `RecallTimelineTool` ~100–122). Empty fields skipped in one path — no `" /  — "` twin. |
| **F2** parallel occurred-at parse | **closed** | Single parse: `ChronologyText.TryParseOccurredAt`. Callers: `DaybookTape.OrderForInject`, `RecallTimelineTool` sort + `InWindow`, `MemoryTape.TryParseOccurredAt` thin delegate. No private twin parser in the tool. |
| **F3** truncate helpers (limits + whitespace) | **closed** | One helper: `ChronologyText.TruncateBody(body, charLimit)` with shared CRLF + double-space collapse. Desk keeps `BodyCharLimit = 280`, tool `400` — intentional limit split, one normalize path. |

## Clean areas

- Shared helper lives in `SoulCore.Memory` — correct layering (Host.Ws + Inference.Tools share without Host→Inference coupling).
- No second inline provenance / truncate / parse implementation on the daybook + `recall_timeline` surface.
- Thin wrappers (`DaybookTape.TruncateBody` → ChronologyText; `MemoryTape.TryParseOccurredAt` → ChronologyText) are intentional adapters, not alias sprawl.

## Notes for PM

- Safe to archive? **yes** — F1–F3 closed; no actionable SLOP on this delta.
- Daybook hygiene chain (PROP-18.2 → 18.13 → 18.17 → 18.20 → 18.26) ready to treat as complete on the slop side.
