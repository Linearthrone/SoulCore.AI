---
type: report
prop_id: PROP-18.24
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.3-FED01-to-PM01.md
task: docs/agents/tasks/PROP-18.24-PM01-to-QA01.md
depends_on_live: PROP-18.6c
---

# PROP-18.24 — QA Presence desk (activities + while-you-were-gone) — Pass

## Result

**Pass.** Presence desk UI contracts for honesty band, While you were gone strip, Activities log five columns, and journal topic tags are covered by client unit tests (6/6). Avalonia surfaces match FED-18.3. Live residue APIs depend on OPS **PROP-18.6c** Host rebuild for the primary `:7700` binary; `:7710` already returns honest empty 200s.

**Ready for SLOP-01** (desk UI code path). Live E2E on `:7700` remains gated on OPS 18.6c.

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| Honesty band — Alive / last tick / last want / last journal; missing = **none yet** (no fabrication) | **Pass** | `FormatHonestyLine_DoesNotFabricate`; `BandHonestyText` in `MainWindow.axaml`; `PresenceResidueFormatter.FormatHonestyLine` |
| Chat-open While you were gone from store; empty honest copy | **Pass** | `ParseWhileYouWereGone_EmptyIsHonest` / `_ListsResidue`; `WhileYouWereGonePanel` + dismiss; empty string *Nothing stored while you were gone — empty is honest.* |
| Activities log columns: Entry, Date/Time, Activity, Subject/Focus, Desc | **Pass** | `ParseActivitiesList_ReadsDeskColumns`; `ActivitiesLogWindow.axaml` header + row bindings |
| Journal topic tags visible in reader; no voice playback | **Pass** | `JournalEntryRow_TopicTagHint_SurfacesTags`; `HerJournalWindow.axaml` copy states tags + no voice |
| Client tests green | **Pass** | 6/6 below |
| Live Host residue routes | **Noted** | `:7700` → **404** (needs OPS 18.6c); `:7710` → **200** empty honest |

## Live API dependency (OPS PROP-18.6c)

Primary Host `127.0.0.1:7700` (`version` 0.1.52) is healthy but **does not** expose residue MapGets yet — same gap FED-18.3 reported. OPS task `PROP-18.6c` must ForceRebuild so `:7700` stops 404ing.

Secondary Host `127.0.0.1:7710` (`version` 1.0.0) already serves the routes with empty-but-honest 200 bodies.

### Probe output (PowerShell `Invoke-WebRequest`)

```text
=== port 7700 ===
/health -> 200
  status=ok service=SoulCore.Host version=0.1.52 soulLoop.enabled=true
/activities?limit=3 -> 404 NotFound
/presence/while-you-were-gone?limit=3 -> 404 NotFound
/journals/topic?subject=optics&limit=3 -> 404 NotFound

=== port 7710 ===
/health -> 200
  status=ok service=SoulCore.Host version=1.0.0
/activities?limit=3 -> 200
  {"count":0,"activities":[]}
/presence/while-you-were-gone?limit=3 -> 200
  {"since":"2026-11-18T22:20:06.906Z","empty":true,"activities":[],"activityCount":0,"journalCrumbs":[],"journalCount":0}
/journals/topic?subject=optics&limit=3 -> 200
  {"subject":"optics","bookId":"thoughts","count":0,"entries":[]}
```

Desk client already treats 404 as honest unavailable copy (`Activities log needs a Host restart (PROP-18.7).`) until 18.6c lands on the desk’s configured Host port.

## Evidence (`dotnet test`)

Command:

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~SoulCoreActivitiesClientTests" --verbosity normal
```

### Client gate (6 tests)

```text
Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseWhileYouWereGone_EmptyIsHonest [22 ms]
Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseActivitiesList_MissingArray_IsProblem [< 1 ms]
Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseActivitiesList_ReadsDeskColumns [3 ms]
Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.JournalEntryRow_TopicTagHint_SurfacesTags [< 1 ms]
Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseWhileYouWereGone_ListsResidue [1 ms]
Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.FormatHonestyLine_DoesNotFabricate [< 1 ms]

Test Run Successful.
Total tests: 6
     Passed: 6
 Total time: 0.8192 Seconds

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Source spot-check (read-only)

| Surface | Location | Behavior |
| --- | --- | --- |
| Honesty band | `MainWindow.axaml` `BandHonestyText`; `MainWindow.Presence.cs` refresh | Host-proven only; formatter never invents tick/want/journal |
| WYWYG strip | `WhileYouWereGonePanel` / `LoadWhileYouWereGoneOnOpenAsync` | `GET /presence/while-you-were-gone?since=`; dismissible; empty honest |
| Activities log | `ActivitiesLogWindow.axaml` | Five columns Entry / Date/Time / Activity / Subject/Focus / Desc; optional subject filter |
| Journal tags | `JournalEntryRow.TopicTagHint` + `HerJournalWindow.axaml` | Tags shown on list line; no voice playback |

## Issues

None opened. `:7700` 404 is tracked as OPS **PROP-18.6c**, not a desk defect.

## Naming

Operator: **LinearThrone**. No forbidden personal names in this report.

## Next

1. PM-01 accept desk QA Pass → dispatch **SLOP-01** on FED-18.3 / desk residue UI.
2. Hold live Presence residue E2E on primary Host until OPS files `PROP-18.6c-OPS01-to-PM01.md` with non-404 probes on `:7700` (or desk is pointed at a Host that already has the routes, e.g. `:7710`).
