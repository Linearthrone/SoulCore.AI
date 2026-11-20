---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.32
prop_id: PROP-18.32
prop_root: PROP-18-chronology-and-felt-persistence
status: clean
created: 2026-11-19
related_slop: docs/agents/reports/PROP-18.25-SLOP01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.29-BED01-to-PM01.md
related_qa: docs/agents/reports/PROP-18.30-QA01-to-PM01.md
---

# SLOP-01 Report — PROP-18.32 (residue F1/F2 re-check)

## Scope

- Related prior SLOP: `docs/agents/reports/PROP-18.25-SLOP01-to-PM01.md` (F1 alias-sprawl, F2 duplicate ¶ split)
- Related cleanup: `docs/agents/reports/PROP-18.29-BED01-to-PM01.md` (Pass)
- Related QA: `docs/agents/reports/PROP-18.30-QA01-to-PM01.md` (Pass — hygiene verified)
- Files / packages scanned:
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (`ActivityLogDto`)
  - `SoulCore/SoulCore.Inference/Presence/ActivityResidueComposer.cs` (`CountParagraphs` / `SplitParagraphs`)
  - `SoulCore/SoulCore.Inference/Presence/ActivityResidueWriter.cs` (¶ floor gate)
  - `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs` (`ActivityDto` / mapping)
  - Desk tests: grep for wire `sessionDesc` (none)
- Method: read primary sources + ripgrep for `sessionDesc` / `CountParagraphs` / dual-wire residue
- **Read-only.** No code modified.

## Summary

- Findings count: **0**
- Highest severity: (n/a)
- Verdict: **clean**
- PROP-18.25 **F1** and **F2** are **closed** via PROP-18.29.

## Residue closure

### F1 — closed (wire `desc` only)

- Host `ActivityLogDto` emits `desc = e.SessionDesc` only; comment documents PROP-18.29 F1 — no `sessionDesc` wire property (`WebApplicationExtensions.cs` ~1297–1308).
- Desk `ActivityDto` / row mapping use `Desc` only; no `SessionDesc` / `sessionDesc` hedge; `FirstNonEmpty` remains only for unrelated journal body preview.
- Grep of desk client + tests: zero wire `sessionDesc` references.
- Store API / SQL still use `session_desc` / `SessionDesc` internally — intentional, not wire alias sprawl.

### F2 — closed (Composer ¶ count reused)

- `ActivityResidueComposer.CountParagraphs` is public and wraps the same private `SplitParagraphs` rules (`ActivityResidueComposer.cs` ~229–240).
- `ActivityResidueWriter.TryWriteAsync` depth-floor gate calls `CountParagraphs` — no twin `"\r\n\r\n"` / `"\n\n"` re-split (`ActivityResidueWriter.cs` ~67–76).

## Clean areas

- No new alias-sprawl or duplicate ¶-split on the PROP-18.7 residue surface after cleanup.
- Prior clean notes from PROP-18.25 (single writer path, Composer owns Desc/journal text, PresenceActivityHub ≠ activities log) still hold; not re-opened.

## Notes for PM

- Safe to archive? **yes** — PROP-18.25 findings cleared; PROP-18.29 + PROP-18.30 + this delta form a complete hygiene chain for the residue writers / activities wire.
- Return token for handoff: **clean**
