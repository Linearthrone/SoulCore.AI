---
type: report
prop_id: PROP-18.16
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.7-BED01-to-PM01.md
task: docs/agents/tasks/PROP-18.16-PM01-to-QA01.md
---

# PROP-18.16 — QA residue writers (log + journal) — Pass

## Result

**Pass.** Non-generic activities-log Desc, topic `thoughts` journal ≥2¶ floor (no upper cap), dual-session distinct residue, and FED-18.3 list/read APIs all verified with real test + HTTP probe output against a current Debug Host build.

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| Non-generic Desc (rejects templates; finding-specific) | **Pass** | `Composer_RejectsGenericDescTemplates`; seed `GENERIC_A/B=False`; API `desc` contains sodium / blaze |
| Topic journal ≥1–2¶ floor, no upper cap | **Pass** | `Composer_JournalFloor_NeverOneLinerTheater`; seed `JOURNAL_A_PARAS=2`; `/journals/topic` bodies have `\n\n` |
| Two sessions same subject, different findings → different Desc/journal | **Pass** | `Composer_TwoSessionsSameSubject_…`; `Writer_TwoPasses_…`; seed `DESC_DISTINCT=True` |
| `GET /activities`, `/activities/{entryNo}` | **Pass** | Side Host :7732 HTTP 200 — count=2, desk columns present |
| `GET /journals/topic?subject=` | **Pass** | HTTP 200 topic crumbs; missing subject → HTTP 400 `subject required` |
| `GET /presence/while-you-were-gone` | **Pass** | HTTP 200 `empty=false`, activities + journalCrumbs; `ListSince` unit test Pass |
| Thin findings skip (no theater) | **Pass** | `Writer_SkipsThinFindings_NoTheaterRow` |
| Runner wiring (leisure / research / parked-work) | **Pass** | Source spot-check: `LeisureRunner` + `AssignedWorkRunner` call `IActivityResidueWriter.TryWriteAsync` |

## Evidence (`dotnet test`)

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~Prop187ActivityResidueTests"
```

```text
Passed Prop187ActivityResidueTests.Composer_JournalFloor_NeverOneLinerTheater
Passed Prop187ActivityResidueTests.Composer_RejectsGenericDescTemplates
Passed Prop187ActivityResidueTests.Writer_SkipsThinFindings_NoTheaterRow
Passed Prop187ActivityResidueTests.Writer_TwoPasses_PersistDistinctDescAndLinkedJournal
Passed Prop187ActivityResidueTests.Store_ListSince_FiltersWhileYouWereGoneWindow
Passed Prop187ActivityResidueTests.Composer_TwoSessionsSameSubject_DifferentFindings_DifferentDescAndJournal

Total tests: 6
     Passed: 6
```

## Evidence (writer seed → side Host HTTP)

Isolated Debug Host on `127.0.0.1:7732` (own `.env` / temp DB — avoids repo `.env` forcing `:7710`). Seeded via `ActivityResidueWriter` (same path as production DI).

### Seed output

```text
WRITE_A null=False entryNo=1 journalId=1
DESC_A=On diffraction gratings: The 600-line grating split the sodium D lines more cleanly than yesterday's cheap film; I could finally see the orange doublet without squinting.
JOURNAL_A_PARAS=2
WRITE_B null=False entryNo=2 journalId=2
DESC_B=On diffraction gratings: Tonight the blaze angle mattered — tilting the mount by two degrees killed the ghost order that had been smearing green into the blue channel.
GENERIC_A=False
GENERIC_B=False
DESC_DISTINCT=True
```

### API probes (`http://127.0.0.1:7732`)

```text
GET /activities?limit=5
HTTP 200
{"count":2,"activities":[{"entryNo":2,"activity":"research","subject":"diffraction gratings","desc":"On diffraction gratings: Tonight the blaze angle mattered — …"},{"entryNo":1,"activity":"leisure","subject":"diffraction gratings","desc":"On diffraction gratings: The 600-line grating split the sodium D lines …","journalBookId":"thoughts","journalEntryId":1}]}

GET /activities/1
HTTP 200
{"activity":{"entryNo":1,"activity":"leisure","subject":"diffraction gratings","desc":"On diffraction gratings: The 600-line grating split the sodium D lines …","journalBookId":"thoughts","journalEntryId":1}}

GET /journals/topic
HTTP 400
{"error":"subject required"}

GET /journals/topic?subject=diffraction%20gratings&limit=5
HTTP 200
{"subject":"diffraction gratings","bookId":"thoughts","count":2,"entries":[{"id":2,"body":"I spent time researching diffraction gratings. …\n\nTonight the blaze angle mattered — …","tagsJson":"[\"topic\",\"activity-residue\",\"research\",\"diffraction gratings\"]"},{"id":1,"body":"I lingered with diffraction gratings between talks. …\n\nThe 600-line grating split the sodium D lines …","tagsJson":"[\"topic\",\"activity-residue\",\"leisure\",\"diffraction gratings\"]"}]}

GET /presence/while-you-were-gone?since=2026-11-01T00:00:00.000Z&limit=10
HTTP 200
{"since":"2026-11-01T00:00:00.000Z","empty":false,"activityCount":2,"journalCount":2,…}
```

Full probe transcript: `tmpcode/prop-18.16-api-probe.txt`.

## Ops note (not a PROP-18.7 code fail)

Loopback Hosts already running on **:7700** and **:7710** still return **HTTP 404** for `/activities`, `/journals/topic`, `/presence/while-you-were-gone` — those processes predate this build. FED-18.3 against live loopback needs OPS rebuild/restart of Host. Current-source Host (QA side probe) serves the routes correctly.

## Next

- PM-01: dispatch **SLOP-01** on PROP-18.7 residue writers / API surface.
- OPS: restart Host from current build so Identity / Presence desk can hit the new endpoints on the primary loopback port.
