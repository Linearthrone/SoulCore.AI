---
type: report
prop_id: PROP-20.20
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.20
status: clean
created: 2026-11-17
related_qa: docs/agents/reports/PROP-20.19-QA01-to-PM01.md
prior_slop: docs/agents/reports/PROP-20.17-SLOP01-to-PM01.md
prior_dev: docs/agents/reports/PROP-20.18-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-20.20

## Scope

- Related QA / DEV: PROP-20.19 (Pass), PROP-20.18 BED cleanup of PROP-20.17 F1/F2
- Files / packages scanned:
  - `SoulCore/SoulCore.Core/Charter/` (presence of `CharterFloorMap.cs`)
  - Repo-wide `*.cs` for `CharterFloorMap`
  - `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` (`ProvisionalFloors` / `ConstitutionTitles`)
  - Spot-check: `ChatContextBuilder` / `VictoriaMindBlockBuilder` (F3 still parallel — deferred)
- Method: read + ripgrep; compare to PROP-20.17 findings + PROP-20.18/20.19 evidence

## Summary

- Findings count: **0** (open)
- Highest severity: n/a
- **F1 closed** · **F2 closed** · **F3 deferred OK**

## Verification

### F1 — Dead `CharterFloorMap` facade — **closed**

- `CharterFloorMap.cs` absent under `SoulCore.Core/Charter/` (dir has `CharterBooks.cs`, `CharterService.cs` only)
- `rg --glob "*.cs" CharterFloorMap` → **no matches**
- Callers remain on `CharterBooks` only

### F2 — Leisure provisional title hardcoding — **closed**

- `LeisureMindBlock.BuildProvisionalFloors` iterates `CharterBooks.ConstitutionTitles`
- Bodies stay in local `ProvisionalFloorBodies` snapshot (documented soft-dep OK)
- No parallel hardcoded title list on the provisional path

### F3 — Parallel Constitution floor load — **still deferred (OK)**

- Desk (`ChatContextBuilder.LoadConstitutionFloorsSafeAsync`) and SMS (`VictoriaMindBlockBuilder.TryBuildAsync`) still both call `ListAnchorDetailsAsync` + `FloorsFromConstitutionAnchors`
- Per PROP-20.18 / PM disposition: leave until a third caller; not blocking this re-check

## Clean areas

- Live floor map remains single-sourced in `CharterBooks`
- No new alias facade reintroduced for book/title resolve
- Provisional leisure titles cannot drift from `ConstitutionTitles` independently

## Notes for PM

- Safe to archive? **yes** — PROP-20.17 F1/F2 cleared; F3 deferred by design
- Hygiene chain complete for step-4; no further SLOP findings on this delta
)
