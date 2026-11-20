---
type: report
prop_id: PROP-20.15
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.14
floor_map: docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md
---

# PROP-20.15 — Constitution floors from books — Pass

## Verdict

Desk chat / SMS mind-block compose loads **Constitution book floors only**. Compass / Personality / Attributes stay out of the preamble. Soft-dep on PROP-20.14: when `charter_anchors.book` is missing, floors resolve from the locked title map (unknown titles fail closed). No Constitution floor weights.

## Done

1. **`CharterBooks` + `CharterFloorMap` facade** — locked 18-title → book map; `IsConstitutionFloor` / `ConstitutionTitles` (five floors).
2. **`VictoriaMindBlock.FloorsFromConstitutionAnchors`** — title + floor line only; schema book wins when present; no erase weights.
3. **`ChatContextBuilder` / `VictoriaMindBlockBuilder`** — compose via Constitution filter; Compass not dumped into preamble.
4. **`GET /settings/identity`** — cheap `book` on each anchor (`ResolveForTitle`).
5. **`LeisureMindBlock` provisional floors** — five Constitution titles (no Honesty/No-harm/Compass dump).
6. **`CharterService` soft-dep** — probe `book` column; filter/seed without column when 20.14 not applied yet.

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests --filter
  "FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~CharterService|FullyQualifiedName~CharterFourBook|FullyQualifiedName~Prop17Leisure"

Passed!  - Failed: 0, Passed: 59, Skipped: 0
```

Covered:

- Five Constitution titles in floors; Honesty / No-harm / Name / Promise / unknown dropped
- Schema book override preferred over title map
- Chat preamble excludes Compass/Personality floor lines
- Leisure mind block still carries floors + seven resting Mid defaults
- Charter 18-title book map + migration 013 soft-compat (DBD 20.14)

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/Charter/CharterBooks.cs` | Four-book ids + floor map + Constitution filter |
| `SoulCore/SoulCore.Core/Charter/CharterFloorMap.cs` | Compat facade for 20.14 call sites |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | `FloorsFromConstitutionAnchors` |
| `SoulCore/SoulCore.Core/Charter/CharterService.cs` | Soft-dep book column + book filter |
| `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` | Constitution-only floors |
| `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` | Same |
| `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` | Identity DTO `book` |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | Provisional Constitution floors |
| `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` | 20.15 floor tests |
| `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` | Constitution-only preamble |

## Acceptance

- [x] Desk chat preamble Constitution floors match the five Constitution titles
- [x] No weight on Constitution that can erase a line
- [x] Report: `docs/agents/reports/PROP-20.15-BED01-to-PM01.md`

## Handoff

- Compass-in-prompt remains **Open** (not in preamble).
- PROP-20.14 owns durable schema/seed truth; this ticket soft-starts against the decision map when `book` is absent.
