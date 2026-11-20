---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.25
prop_id: PROP-18.25
prop_root: PROP-18-chronology-and-felt-persistence
status: findings
created: 2026-11-19
related_qa: docs/agents/reports/PROP-18.16-QA01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.7-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.25 (PROP-18.7 residue writers + activities APIs)

## Scope

- Related QA: `docs/agents/reports/PROP-18.16-QA01-to-PM01.md` (Pass — Ready for SLOP-01)
- Related BED: `docs/agents/reports/PROP-18.7-BED01-to-PM01.md`
- Files / packages scanned:
  - `SoulCore/SoulCore.Inference/Presence/ActivityResidueComposer.cs`
  - `SoulCore/SoulCore.Inference/Presence/IActivityResidueWriter.cs`
  - `SoulCore/SoulCore.Inference/Presence/ActivityResidueWriter.cs`
  - `SoulCore/SoulCore.Memory/IVictoriaActivitiesLogStore.cs`
  - `SoulCore/SoulCore.Memory/Repositories/SqliteVictoriaActivitiesLogRepository.cs`
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (`/activities`, `/activities/{entryNo}`, `/journals/topic`, `/presence/while-you-were-gone`, `ActivityLogDto`, `JournalTagsContainSubject`)
  - `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs` (`WriteActivityResidueAsync` + adjacent `WriteJournalNoteAsync`)
  - `SoulCore/SoulCore.Host/Loop/AssignedWorkRunner.cs` (`WriteParkedWorkResidueAsync`)
  - `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/CompanionServiceCollectionExtensions.cs` (DI)
  - Adjacent (alias check only): `PresenceActivityHub.cs`, `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs` (`desc`/`sessionDesc` consumers)
  - Tests: `SoulCore/SoulCore.Protocol.Tests/Prop187ActivityResidueTests.cs`
- Method: read primary sources + ripgrep for `ActivityResidueComposer` / `IActivityResidueWriter` / `TryWriteAsync` / `ListSinceAsync` / `ActivityLogDto` / `JournalTagsContainSubject` / route MapGets
- **Read-only.** No code modified.

## Summary

- Findings count: **2**
- Highest severity: **P2**
- Verdict: **findings**

## Findings

### F1 — Wire DTO emits both `desc` and `sessionDesc` (same value)

- Category: alias-sprawl
- Severity: **P2**
- Evidence:
  - `WebApplicationExtensions.ActivityLogDto` sets `desc = e.SessionDesc` and `sessionDesc = e.SessionDesc` (`WebApplicationExtensions.cs` ~1297–1308).
  - Desk / BED / QA prose all name the column **Desc**; store field is `session_desc` / `SessionDesc`.
  - FED client already hedges with `FirstNonEmpty(a.Desc, a.SessionDesc)` (`SoulCoreActivitiesClient.cs` ~277, `ActivityDto` ~346–350).
- Why it matters: one purpose, two JSON names → clients and probes diverge; future field renames or partial serializers can leave one side stale.
- Recommended action: **remove** (keep a single wire name)
- Suggested owner: **BED-01** (Host DTO); FED may drop `SessionDesc` fallback after Host settles
- Ask-user question: Prefer wire name `desc` (desk / QA language) or `sessionDesc` (store property)? Recommend **`desc` only**.

### F2 — Paragraph-floor check reimplements Composer split

- Category: duplicate
- Severity: **P2**
- Evidence:
  - `ActivityResidueComposer.SplitParagraphs` is private and already used for the ≥2¶ journal floor (`ActivityResidueComposer.cs` ~117–155, ~229–234).
  - `ActivityResidueWriter.TryWriteAsync` re-splits with the same `"\r\n\r\n"` / `"\n\n"` separators to count paragraphs before persist (`ActivityResidueWriter.cs` ~67–78).
- Why it matters: mild; if split rules ever change (e.g. trim / empty-para policy), Writer gate and Composer body can disagree and refuse valid residue or accept theater.
- Recommended action: **dedupe**
- Suggested owner: **BED-01**
- Ask-user question: (n/a) — expose something like `ActivityResidueComposer.CountParagraphs(string)` (or reuse `SplitParagraphs`) and call it from the Writer.

## Clean areas

- **Single residue write path:** only `IActivityResidueWriter` / `ActivityResidueWriter`; one DI registration; Leisure + AssignedWork call `TryWriteAsync` with distinct kinds (`leisure`/`research` vs `parked-work`) — thin wrappers, not parallel composers.
- **Composer owns Desc/journal text:** no second `BuildSessionDesc` / generic-template rejector elsewhere in Host/Inference.
- **`PresenceActivityHub` ≠ activities log:** live HUD “doing now” vs inspectable residue — different purpose, not alias sprawl.
- **Leisure dual journal books are intentional:** `life_hacks` + `["r&r","untrusted"]` (`WriteJournalNoteAsync`) vs `thoughts` + activity-residue tags (Writer) — different books/tags, not same-purpose aliases.
- **Store API:** `ListRecentAsync` / `GetAsync` / `ListSinceAsync` are distinct; SQL is shared via `ReadEntry` — no copy-pasted repository twin.
- **Routes:** four MapGets are thin; shared `ActivityLogDto`; `/journals/topic` over-fetch-then-filter is pragmatic given no tag-index store API (not flagged as removable slop).
- **Comments:** PROP-18.7 / Q1–Q4 notes document product law; not restate-the-line noise.

## Notes for PM

- Safe to archive? **no** — two P2 cleanups recommended before treating PROP-18.7 surface as hygiene-complete (non-blocking for product ship if you accept risk / defer).
- If findings: prefer BED cleanup of F1 (+ optional F2) before user-facing “done”; F1 also lightly touches FED client after Host settles.
- QA Pass (PROP-18.16) stands; these are hygiene only, not functional fails.
- OPS note from QA (loopback Hosts 404 until rebuild) is out of SLOP scope.
