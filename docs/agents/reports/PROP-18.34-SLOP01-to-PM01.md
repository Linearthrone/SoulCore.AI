---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.34
prop_id: PROP-18.34
prop_root: PROP-18-chronology-and-felt-persistence
status: clean
created: 2026-11-19
related_task: docs/agents/tasks/PROP-18.34-PM01-to-SLOP01.md
related_slop: docs/agents/reports/PROP-18.27-SLOP01-to-PM01.md
related_fed: docs/agents/reports/PROP-18.31-FED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.34 (desk F1/F2 closed?)

## Scope

- Prior findings: `docs/agents/reports/PROP-18.27-SLOP01-to-PM01.md` (F1 P1 dead topic client; F2 P2 honesty Desc fallback)
- Cleanup: `docs/agents/reports/PROP-18.31-FED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs`
  - `House/House.ChatDesktop/MainWindow.Presence.cs` (`LoadWhileYouWereGoneOnOpenAsync`, `RefreshHonestyBand`)
  - `House/House.ChatDesktop.Tests/SoulCoreActivitiesClientTests.cs`
  - Ripgrep under `House/` for F1 symbols + F2 Desc-fallback patterns
- Method: read + ripgrep (re-audit only; no full Presence desk re-scan)
- **Read-only.** No code modified.

## Summary

- Findings count: **0**
- Highest severity: n/a
- Verdict: **clean**
- PROP-18.27 **F1** and **F2** are **closed**.

## Closure evidence

### F1 — Unused `/journals/topic` client surface — **closed**

- Ripgrep `House/`: **no** matches for `ListTopicAsync`, `ParseTopicJournal`, `TopicJournalFetch`, `TopicJournalDto`, or `journals/topic` on the desk client.
- `SoulCoreActivitiesClient` summary documents only `GET /activities` + `GET /presence/while-you-were-gone`; topic tags remain via Her journal (`JournalEntryRow.TopicTagHint`).
- Matches PROP-18.31 FED claim (dead path removed; Host `/journals/topic` untouched).

### F2 — Honesty “Last journal” activity Desc fallback — **closed**

- `LoadWhileYouWereGoneOnOpenAsync` (`MainWindow.Presence.cs` ~916–918):

  ```csharp
  // PROP-18.31 F2: Last journal is journal crumbs only — no activity Desc fallback.
  var journal = PresenceResidueFormatter.NewestJournalPreview(fetch);
  _lastJournalCrumb = string.IsNullOrWhiteSpace(journal) ? null : journal;
  ```

- No path sets `_lastJournalCrumb` from `Activities[0].DescText`.
- `FormatHonestyLine` still labels the slot **Last journal**; empty crumb → `none yet` (test `FormatHonestyLine_DoesNotFabricate`).
- Regression test `NewestJournalPreview_ActivitiesOnly_DoesNotUseDesc`: activities-only WYWYG → `NewestJournalPreview` null; honesty line has `Last journal — none yet` and does not contain the Desc text.

## Clean areas (delta)

- No new dead topic-journal wire reintroduced in the cleanup surface.
- Honesty band journal slot is journal-crumb-only; label matches data source.
- Prior PROP-18.27 clean areas (WYWYG honesty, Activities columns, `TopicTagHint`) not re-opened; out of delta unless regression symbols appear (none found).

## Notes for PM

- Safe to archive? **yes** — F1/F2 hygiene complete for this desk delta; PROP-18.27 + PROP-18.31 + PROP-18.34 form a closed SLOP chain for those findings.
- No DEV re-ticket required from this re-audit.
- Host `desc`/`sessionDesc` dual wire (PROP-18.25 F1) remains out of scope / not re-opened.
