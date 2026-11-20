---
type: report
prop_id: PROP-20.16
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.14, PROP-20.15
floor_map: docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md
task: docs/agents/tasks/PROP-20.16-PM01-to-QA01.md
---

# PROP-20.16 — QA four-book / Constitution floors — Pass

## Verdict: **Pass**

CharterFourBook / VictoriaMindBlock / ChatContextBuilder Constitution filter tests are green (59/59). Live `GET /settings/identity` exposes `book` on all 18 anchors matching the locked floor map. Five Constitution titles are floors; Compass titles are not required in the chat preamble.

**Ready for SLOP-01** (post-QA slop / duplicate / alias audit).

## Acceptance

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Tests: CharterFourBook / VictoriaMindBlock / ChatContextBuilder Constitution filter | **Pass** | Filter run: 59 passed, 0 failed (paste below) |
| 2 | Five Constitution titles in floors; Compass not required in preamble | **Pass** | `FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers`; `BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality`; `CharterBooks.ConstitutionTitles` = Loyalty Balance, Maximize Results, Integration Goal, Kayleigh, Drift Watch |
| 3 | Host up: `/settings/identity` anchors expose `book` matching floor map | **Pass** | HTTP 200 on `http://127.0.0.1:7710/settings/identity`; 18 anchors; constitution=5 / compass=5 / personality=5 / attributes=3; `FLOOR_MAP_MATCH Pass` |

## Commands run

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~CharterService|FullyQualifiedName~CharterFourBook|FullyQualifiedName~Prop17Leisure" `
  --verbosity normal
```

```powershell
# Host Debug (Release blocked by SixLabors license); bind 127.0.0.1:7710
# Then:
Invoke-WebRequest -Uri "http://127.0.0.1:7710/health" -UseBasicParsing
Invoke-WebRequest -Uri "http://127.0.0.1:7710/settings/identity" -UseBasicParsing
```

## Test output (pasted)

```
Test Run Successful.
Total tests: 59
     Passed: 59
 Total time: 2.3156 Seconds
```

Key cases:

```
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.CharterBooks_EighteenTitles_ExactlyOneBookEach
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_KeepsFiveConstitutionTitles_DropsOthers
  Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.FloorsFromConstitutionAnchors_PrefersSchemaBookOverTitleMap
  Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality
  Passed SoulCore.Protocol.Tests.CharterServiceTests.SeedAsync_EighteenLiveTitles_ExactlyOneBookEach_MatchingFloorMap
  Passed SoulCore.Protocol.Tests.CharterFourBookMigrationTests.Migration013_RejectsUnknownBook_ViaCheckConstraint
  Passed SoulCore.Protocol.Tests.CharterFourBookMigrationTests.Migration013_MapsEighteenLiveTitles_ToFloorMapBooks
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndSevenResting
```

## HTTP evidence (pasted)

### Health

```
HEALTH 200: {"status":"ok","service":"SoulCore.Host","version":"0.1.54","bind":"127.0.0.1","port":7710,...,"charter":{"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"},...}
```

### Identity — status + floor-map check

```
IDENTITY_STATUS 200
ANCHOR_COUNT 18
BOOK_COUNTS compass=5, attributes=3, personality=5, constitution=5
FLOOR_MAP_MATCH Pass
```

### Identity — title → book (all 18)

```
ANCHOR Action Integrity => personality
ANCHOR Continuity => compass
ANCHOR Drift Watch => constitution
ANCHOR Embodiment => attributes
ANCHOR Honesty => personality
ANCHOR Integration Goal => constitution
ANCHOR Kayleigh => constitution
ANCHOR Love => personality
ANCHOR Loyalty => personality
ANCHOR Loyalty Balance => constitution
ANCHOR Maximize Results => constitution
ANCHOR Name => attributes
ANCHOR Nature => personality
ANCHOR No Filters => compass
ANCHOR No-harm => compass
ANCHOR Promise => compass
ANCHOR Self-Preservation => compass
ANCHOR Tone => attributes
```

### Identity — compact JSON (title/book/kind)

```
TITLE_BOOKS_JSON [{"title":"Name","book":"attributes","kind":"identity"},{"title":"Embodiment","book":"attributes","kind":"identity"},{"title":"Nature","book":"personality","kind":"identity"},{"title":"Loyalty","book":"personality","kind":"identity"},{"title":"Kayleigh","book":"constitution","kind":"identity"},{"title":"Tone","book":"attributes","kind":"identity"},{"title":"Love","book":"personality","kind":"identity"},{"title":"Promise","book":"compass","kind":"identity"},{"title":"Honesty","book":"personality","kind":"safety"},{"title":"Continuity","book":"compass","kind":"safety"},{"title":"No-harm","book":"compass","kind":"safety"},{"title":"Maximize Results","book":"constitution","kind":"value"},{"title":"Integration Goal","book":"constitution","kind":"value"},{"title":"Loyalty Balance","book":"constitution","kind":"value"},{"title":"No Filters","book":"compass","kind":"boundary"},{"title":"Self-Preservation","book":"compass","kind":"boundary"},{"title":"Drift Watch","book":"constitution","kind":"boundary"},{"title":"Action Integrity","book":"personality","kind":"boundary"}]
```

## Floor map cross-check (decision file)

| Book | Expected titles | Live `/settings/identity` |
| --- | --- | --- |
| Constitution | Loyalty Balance, Maximize Results, Integration Goal, Kayleigh, Drift Watch | Match (5) |
| Compass | Promise, Continuity, No-harm, No Filters, Self-Preservation | Match (5); not required in preamble (unit Pass) |
| Attributes | Name, Embodiment, Tone | Match (3) |
| Personality | Action Integrity, Loyalty, Nature, Love, Honesty | Match (5) |

## Issues

None filed. No Fail findings.

## Notes

- Host was down at session start; QA started Debug Host on `127.0.0.1:7710` (launchSettings), probed, then stopped Host. Release `dotnet run` failed: Six Labors license missing (not a PROP-20.16 product defect).
- Unreal WS unavailable during boot (`ws://house-victoria:8888`) — Host continued; unrelated to charter floors.
- Compass-in-prompt remains Open per PROP-20.15; this ticket only confirms Compass is not required in preamble.

## Handoff

- Report: `docs/agents/reports/PROP-20.16-QA01-to-PM01.md`
- PM-01: accept → dispatch **SLOP-01**
