---
type: report
prop_id: PROP-18.31
prop_root: PROP-18-chronology-and-felt-persistence
from: FED-01
to: PM-01
status: Pass
created: 2026-11-19
depends_on: PROP-18.27
task: docs/agents/tasks/PROP-18.31-PM01-to-FED01.md
related_slop: docs/agents/reports/PROP-18.27-SLOP01-to-PM01.md
related_accept: docs/agents/reports/PROP-18.27-PM01-accept.md
---

# PROP-18.31 — Desk hygiene (F1–F2) — Pass

## Verdict: **Pass**

PM-locked hygiene from PROP-18.27 applied: dead topic-journal client removed; honesty band **Last journal** no longer falls back to activity Desc (`none yet` when no crumb).

## Done

### F1 — Remove dead `/journals/topic` desk client

Removed from `SoulCoreActivitiesClient`:

- `ListTopicAsync`
- `ParseTopicJournal`
- `TopicJournalFetch`
- `TopicJournalDto`

Client summary now documents only `/activities` + `/presence/while-you-were-gone`. Topic tags remain via Her journal (`JournalEntryRow.TopicTagHint`). Host `/journals/topic` untouched (BED).

Ripgrep `House/`: **no** remaining `ListTopicAsync` / `ParseTopicJournal` / `TopicJournalFetch` / `TopicJournalDto`.

### F2 — Honesty Last journal = journal crumbs only

`LoadWhileYouWereGoneOnOpenAsync` (`MainWindow.Presence.cs`):

- Before: empty `NewestJournalPreview` → `_lastJournalCrumb = fetch.Activities[0].DescText`
- After: empty preview → `_lastJournalCrumb = null` → `FormatHonestyLine` renders **Last journal — none yet**

## Acceptance

| Criterion | Result |
| --- | --- |
| Dead `ListTopicAsync` / topic DTO path removed | Pass |
| No activity Desc under Last journal | Pass |
| Empty journal crumb → `none yet` | Pass |
| Tests updated | Pass |

## Evidence

```
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Debug
  --filter "FullyQualifiedName~SoulCoreActivitiesClientTests"
  → Test Run Successful.
     Total tests: 7
     Passed: 7
```

Cases: desk columns parse; missing activities[] is problem; empty WYWYG honest; residue list + journal preview; honesty no fabrication; topic tag hint; **NewestJournalPreview_ActivitiesOnly_DoesNotUseDesc** (PROP-18.31 F2).

Note: full suite had 3 unrelated CompanionToken* failures (env/file lock from running ChatDesktop / local token file) — not in PROP-18.31 scope. Release rebuild blocked while `House.ChatDesktop` PID 37368 holds the Release DLL; Debug build + filtered tests verified the hygiene changes.

## Files

- `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs`
- `House/House.ChatDesktop/MainWindow.Presence.cs`
- `House/House.ChatDesktop.Tests/SoulCoreActivitiesClientTests.cs`
- `docs/agents/reports/PROP-18.31-FED01-to-PM01.md`
