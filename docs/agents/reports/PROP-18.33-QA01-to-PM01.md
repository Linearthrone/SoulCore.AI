---
type: report
prop_id: PROP-18.33
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.31-FED01-to-PM01.md
task: docs/agents/tasks/PROP-18.33-PM01-to-QA01.md
depends_on: PROP-18.31
---

# PROP-18.33 — QA desk hygiene (topic client gone + Last journal) — Pass

## Result

**Pass.** PROP-18.31 verified: dead `/journals/topic` desk client symbols are gone from `House/`; honesty **Last journal** uses journal crumbs only (activities-only → `none yet`, never activity `Desc`). SoulCoreActivitiesClientTests **7/7** green.

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| No `ListTopicAsync` / topic DTO path in desk | **Pass** | Ripgrep `House/` for `ListTopicAsync\|ParseTopicJournal\|TopicJournalFetch\|TopicJournalDto` → **no matches** |
| Last journal never uses activity Desc | **Pass** | `MainWindow.Presence.cs` ~917–918: `_lastJournalCrumb` from `NewestJournalPreview` only; empty → `null` |
| Activities-only → `Last journal — none yet` | **Pass** | `NewestJournalPreview` returns null when `JournalCrumbs.Count == 0`; `FormatHonestyLine` + test below |
| SoulCoreActivitiesClientTests green | **Pass** | 7/7 — real output pasted below |

## Source checks (QA read-only)

### F1 — dead topic client removed

Ripgrep under `House/` (2026-11-19):

```text
pattern: ListTopicAsync|ParseTopicJournal|TopicJournalFetch|TopicJournalDto
→ No matches found
```

Topic tags remain only via Her journal (`JournalEntryRow.TopicTagHint`) — covered by `JournalEntryRow_TopicTagHint_SurfacesTags`.

### F2 — Last journal honesty

`LoadWhileYouWereGoneOnOpenAsync` (`MainWindow.Presence.cs`):

```csharp
// PROP-18.31 F2: Last journal is journal crumbs only — no activity Desc fallback.
var journal = PresenceResidueFormatter.NewestJournalPreview(fetch);
_lastJournalCrumb = string.IsNullOrWhiteSpace(journal) ? null : journal;
```

`NewestJournalPreview` (`SoulCoreActivitiesClient.cs` ~350–357) reads **only** `fetch.JournalCrumbs` — never `Activities[].Desc` / `DescText`.

Regression case `NewestJournalPreview_ActivitiesOnly_DoesNotUseDesc`: activities with desc + empty `journalCrumbs` → preview null → honesty line contains `Last journal — none yet` and does **not** contain activity desc text (`lens papers`).

## Evidence (`dotnet test`)

Workspace: `c:\Users\kurtw\.house_victoria\SoulCore.AI`  
Date: 2026-11-19

Command:

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Debug
  --filter "FullyQualifiedName~SoulCoreActivitiesClientTests" --verbosity normal
```

Output (verbatim):

```text
Test run for C:\Users\kurtw\.house_victoria\SoulCore.AI\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.04]   Discovering: House.ChatDesktop.Tests
[xUnit.net 00:00:00.08]   Discovered:  House.ChatDesktop.Tests
[xUnit.net 00:00:00.08]   Starting:    House.ChatDesktop.Tests
[xUnit.net 00:00:00.14]   Finished:    House.ChatDesktop.Tests
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseWhileYouWereGone_EmptyIsHonest [20 ms]
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseActivitiesList_MissingArray_IsProblem [< 1 ms]
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseActivitiesList_ReadsDeskColumns [3 ms]
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.NewestJournalPreview_ActivitiesOnly_DoesNotUseDesc [2 ms]
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.JournalEntryRow_TopicTagHint_SurfacesTags [< 1 ms]
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.ParseWhileYouWereGone_ListsResidue [1 ms]
  Passed House.ChatDesktop.Tests.SoulCoreActivitiesClientTests.FormatHonestyLine_DoesNotFabricate [< 1 ms]

Test Run Successful.
Total tests: 7
     Passed: 7
 Total time: 0.4518 Seconds

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.12
```

## Issues

None.

## Notes for PM

- Scope limited to desk hygiene claimed in PROP-18.31; Host `/journals/topic` intentionally untouched (BED).
- CSS / live Avalonia band visual not required for this PROP; honesty string logic covered by unit tests.
