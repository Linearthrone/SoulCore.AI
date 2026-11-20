---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.17
prop_id: PROP-18.17
prop_root: PROP-18-chronology-and-felt-persistence
status: findings
created: 2026-11-19
related_qa: docs/agents/reports/PROP-18.13-QA01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.2-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.17 (PROP-18.2 daybook surface)

## Scope

- Related QA: `docs/agents/reports/PROP-18.13-QA01-to-PM01.md` (Pass)
- Related BED: `docs/agents/reports/PROP-18.2-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore/SoulCore.Host/Ws/DaybookTape.cs`
  - `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` (daybook load + inject + budget share)
  - `SoulCore/SoulCore.Inference/Tools/RecallTimelineTool.cs`
  - `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/MemoryServiceCollectionExtensions.cs` (DI)
  - Adjacent: `SoulCore/SoulCore.Host/Ws/MemoryTape.cs` (`TryParseOccurredAt` reuse)
  - Tests: `SoulCore/SoulCore.Protocol.Tests/DaybookTapeTests.cs`
- Method: read primary sources + ripgrep for `DaybookTape` / `recall_timeline` / `FromJournal` / `FromActivity` / `TruncateBody` / `TryParseOccurredAt` / provenance `journal:`
- **Read-only.** No code modified.

## Summary

- Findings count: **3**
- Highest severity: **P1**
- Verdict: **findings**

## Findings

### F1 — Journal/activity provenance mapping duplicated (and activity text diverges)

- Category: duplicate
- Severity: **P1**
- Evidence:
  - `DaybookTape.FromJournal` builds `journal:{book}` / `journal:{book}/{source}` (`DaybookTape.cs` ~49–64).
  - `RecallTimelineTool.ExecuteAsync` inlines the same provenance formula (`RecallTimelineTool.cs` ~99–103) instead of calling a shared mapper.
  - Activity crumbs: `DaybookTape.FromActivity` carefully skips empty activity/subject/desc (`DaybookTape.cs` ~67–88); tool does naive `Activity + " / " + Subject + " — " + SessionDesc` (`RecallTimelineTool.cs` ~116–117) → empty fields become `" /  — "` junk in tool output while `[Daybook]` stays clean.
  - Layering: `SoulCore.Inference` cannot reference `SoulCore.Host.Ws.DaybookTape` (Host → Inference). Shared helpers belong in Memory/Core (or similar), not a Host call from the tool.
- Why it matters: same chronology surface, two formatters → provenance/text can drift; activity path already diverges on empty fields.
- Recommended action: **dedupe**
- Suggested owner: **BED-01**
- Ask-user question: (n/a)

### F2 — Parallel occurred-at parse in `RecallTimelineTool`

- Category: alias-sprawl
- Severity: **P2**
- Evidence:
  - `DaybookTape.OrderForInject` correctly reuses `MemoryTape.TryParseOccurredAt` (`DaybookTape.cs` ~130).
  - `RecallTimelineTool` keeps a private `TryParse` with the same invariant-culture / AssumeUniversal logic (`RecallTimelineTool.cs` ~198–208 vs `MemoryTape.cs` ~153–166).
  - Same layering constraint as F1: tool cannot call `MemoryTape` in Host.Ws today.
- Why it matters: third copy of “parse store stamp” if other Inference tools grow chronology; window filter and daybook order can disagree if one copy is fixed later.
- Recommended action: **dedupe**
- Suggested owner: **BED-01**
- Ask-user question: (n/a) — prefer one shared parse helper next to the F1 mapper (Memory/Core), then wire both DaybookTape and RecallTimelineTool.

### F3 — Truncate helpers: same purpose, different limits/behavior

- Category: alias-sprawl
- Severity: **P2**
- Evidence:
  - `DaybookTape.TruncateBody` — `BodyCharLimit = 280`, collapses CRLF + double spaces (`DaybookTape.cs` ~21–22, ~272–284).
  - `RecallTimelineTool.Truncate` — `BodyCharLimit = 400`, only strips `\n`/`\r` (`RecallTimelineTool.cs` ~17, ~211–217).
  - Both append `…` on overflow; both serve provenance-tagged chronology text.
- Why it matters: mild; different caps may be intentional (preamble soft-cap vs on-demand tool), but whitespace collapse already diverges for the same journal body.
- Recommended action: **ask-user**
- Suggested owner if remove/dedupe: **BED-01**
- Ask-user question: Keep 280 (preamble) vs 400 (tool) as intentional, or share one truncate helper with a `limit` parameter and one whitespace-normalize path?

## Clean areas

- **Single preamble tape:** only `DaybookTape.BuildDaybookBlock` / `OrderForInject`; `ChatContextBuilder` delegates (budget `Min(2400, remaining/3)`, inject before `[Memory]`).
- **Single tool registration:** one `AddSingleton<ITool, RecallTimelineTool>()` in memory DI; name `recall_timeline` only.
- **Soft-fail load matches memories:** `LoadDaybookLinesSafeAsync` try/catch empty is consistent with episodic soft-fail — not abnormal defensive slop.
- **PROP ticket comments** are local convention; not flagged as style noise.
- **No second daybook inject path** on SMS (intentional PROP-18.2 out-of-scope; not re-flagged).
- **Preferred-book / journal-vs-crumb slot caps** live in one place (`DaybookTape` constants).

## Notes for PM

- Safe to archive? **no** — prefer BED cleanup of **F1** (shared journal/activity → provenance+text) before user-facing “done” on PROP-18.2 → 18.13 → 18.17.
- **F2** folds naturally into the same shared chronology helper move.
- **F3** can defer / ask LinearThrone; not blocking if limits stay intentional.
- After BED cleanup: OPS → QA as PM prefers; re-ticket SLOP only if the shared helper move grows beyond daybook + `recall_timeline`.
