---
type: report
prop_id: PROP-20.17
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.17
status: findings
created: 2026-11-17
related_qa: docs/agents/reports/PROP-20.16-QA01-to-PM01.md
---

# SLOP-01 Report — PROP-20.17

## Scope

- Related QA / DEV: PROP-20.16 (Pass), PROP-20.15 BED, PROP-20.14 DBD
- Files / packages scanned:
  - `SoulCore/SoulCore.Core/Charter/CharterBooks.cs`
  - `SoulCore/SoulCore.Core/Charter/CharterFloorMap.cs`
  - `SoulCore/SoulCore.Core/Charter/CharterService.cs` (book soft-dep / filter)
  - `SoulCore/SoulCore.Core/VictoriaMindBlock.cs`
  - `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs`
  - `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs`
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (identity `book`)
  - `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` (provisional floors)
  - `SoulCore/SoulCore.Memory/Migrations/013_charter_four_books.sql`
  - Related tests under `SoulCore.Protocol.Tests` (VictoriaMindBlock / ChatContextBuilder / CharterFourBook / CharterService / Prop17Leisure)
- Method: read + ripgrep for `CharterFloorMap` / `CharterBooks` / `FloorsFromConstitution` / `FloorsFromAnchors` / `IsConstitutionFloor` / `ResolveForTitle` / Constitution title literals; skim PROP-20.14/20.15 reports

## Summary

- Findings count: **3**
- Highest severity: **P1**

Constitution floor filter path itself is tight (`CharterBooks` → `FloorsFromConstitutionAnchors` → desk/SMS builders). Main slop is a **dead alias facade** and two mild duplicates on the change surface.

## Findings

### F1 — Dead `CharterFloorMap` facade (zero call sites)

- Category: alias-sprawl
- Severity: **P1**
- Evidence:
  - `SoulCore/SoulCore.Core/Charter/CharterFloorMap.cs` — thin wrap of `CharterBooks` (`IsValidBook` ↔ `IsAllowed`, `TryResolveBook` vs `ResolveForTitle`)
  - Repo-wide ripgrep: **no** `.cs` references outside the type’s own file (only PROP-20.14/20.15/20.17 docs mention it)
  - Product path uses `CharterBooks` only (`CharterService`, `VictoriaMindBlock`, identity DTO)
- Why it matters: Same-purpose dual API with **different unknown-title semantics** (`TryResolveBook` fail-closed vs `ResolveForTitle` → Constitution). Dead facade invites the next ticket to pick the wrong helper.
- Recommended action: **remove**
- Suggested owner: BED-01

### F2 — `LeisureMindBlock.ProvisionalFloors` hardcodes five Constitution titles

- Category: duplicate
- Severity: **P2**
- Evidence:
  - `LeisureMindBlock.cs` `ProvisionalFloors` literals: Loyalty Balance / Maximize Results / Integration Goal / Kayleigh / Drift Watch
  - Same ordered set already owned by `CharterBooks.ConstitutionTitles` / `FloorMapByTitle`
  - Bodies are a provisional snapshot of charter text (intentional soft-dep when floors not injected)
- Why it matters: Title list can drift from the locked floor map if either side is edited alone; bodies drifting is less critical for provisional fallback.
- Recommended action: **dedupe** (titles from `CharterBooks.ConstitutionTitles`; keep provisional body strings local, or document “snapshot OK”)
- Suggested owner: BED-01

### F3 — Parallel Constitution floor load (desk vs SMS builder)

- Category: duplicate
- Severity: **P2**
- Evidence:
  - `ChatContextBuilder.LoadConstitutionFloorsSafeAsync` — `ListAnchorDetailsAsync` + `VictoriaMindBlock.FloorsFromConstitutionAnchors`
  - `VictoriaMindBlockBuilder.TryBuildAsync` — same two calls, then `VictoriaMindBlock.Build`
- Why it matters: Filter logic is already shared; only the charter-fetch + map loop is copy-pasted. Drift risk is low today but two swallow/catch styles differ (logged vs bare `catch`).
- Recommended action: **ask-user**
- Ask-user question: Extract a tiny shared “load Constitution floors from `ICharter`” helper now, or defer until a third caller appears?

## Clean areas

- Single source of truth for live map: `CharterBooks.FloorMapByTitle` + migration 013 CASE (SQL must duplicate; acceptable)
- `FloorsFromConstitutionAnchors` correctly gates via `IsConstitutionFloor` (schema book wins; unknown titles fail closed for floors)
- Desk + SMS production paths both use the Constitution filter (not `FloorsFromAnchors`)
- Identity DTO `book` via `CharterBooks.ResolveForTitle` — no second mapper
- No Constitution floor weights / erase-weight column in Core Charter surface
- `FloorsFromAnchors` remains a lower-level unfiltered mapper used by tests only — documented “prefer Constitution” note; not product dual-path

## Notes for PM

- Safe to archive? **no** — at least ticket **F1 remove `CharterFloorMap`** (small BED cleanup). F2 optional same ticket; F3 defer OK.
- If findings: prefer DEV cleanup of F1 before user-facing “step 4 done”; F2/F3 non-blocking.
- Functional QA (PROP-20.16) already Pass — this is hygiene only; no OPS/QA re-run needed unless BED changes behavior of provisional leisure floors.
)
