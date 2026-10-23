---
type: report
prop_id: PROP-16.3b
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.3b-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.3b — Hide an archived local thread — Pass

## Verdict

**Pass.** Archive on a thread row posts `{"summary":"..."}` to `POST /api/personas/{personaId}/archive`. The summary is the trimmed text of the last visible user message, a newline, and the trimmed text of the last visible assistant message, capped at 2000 characters. If both are empty, the body is `Archived.` A failed or thrown post leaves the hide mark where it was. A successful post records the latest row id for that persona key. Loads skip rows at or before the mark. The thread row is omitted until a newer row exists. Rows stay in `presence-chat.db`.

No model call. No trait sliders. No chat-multi. No edits under `SoulCore/`. `ChatTranscriptKey.ForPersona` was already the persona key and was not changed. No commit, no push, no stash. Branch stayed `pr-177`.

The Archive button was not clicked in a running Presence window. Evidence is the build and `ThreadArchiveTests` (7 passed).

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| A failed post does not hide the thread | Pass | `FailedPost_DoesNotHideTheThread` passed. A poster that returns false, and a poster that throws, both leave `LoadRecent` as `keep-user` / `keep-assistant`, `IsThreadListed` true, and `CountStored` 2. `LaterMessage_ShowsOnlyRowsAfterTheMark` then posts false after a new exchange: the mark does not move, and `new-user` / `new-assistant` stay visible. |
| A successful post hides that persona’s rows and drops the thread row | Pass | `SuccessfulPost_HidesRows_DropsThread_AndKeepsThemInTheDb` passed. During the post the thread is still listed. After true, `IsThreadListed` is false and `LoadRecent` is empty. The posted summary is `second\ntwo` (last user and assistant lines, trimmed; the earlier pair and the system row are not the summary). `RefreshThreadList` skips a persona when `IsThreadListed` is false. |
| The hidden rows are still in `presence-chat.db` | Pass | Same test. `CountStored` stays 5. `LoadIncludingHidden` still contains `first`, `  second  `, and `noise`. There is no delete. |
| A later message on that persona shows only the new rows | Pass | `LaterMessage_ShowsOnlyRowsAfterTheMark` passed. After the mark, `LoadRecent` is empty and the thread is unlisted. After `new-user` and `new-assistant`, the thread is listed again and `LoadRecent` is only those two. `LoadIncludingHidden` still has the old pair plus the new pair (4). `EmptyExchange_SendsArchived_AndALaterMessageComesBack` passed for a blank exchange: summary `Archived.`, thread drops, then `after` is the only visible row. |
| Another persona’s transcript is unchanged | Pass | `OtherPersona_TranscriptIsUnchanged` passed. Beta is saved first, so its row ids are below alpha’s mark. After alpha archives, beta’s id, role, and text list matches the snapshot, `CountStored` is unchanged, and beta stays listed. Alpha’s `LoadRecent` is empty. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Output below. 0 warnings, 0 errors. |
| Report lists commands and results | Pass | This file. |

## What changed

- Each thread row has an Archive button. The press does not switch persona. A failed post sets the persona status and leaves the thread up. A successful post does not write a chat line, reloads the open transcript when it is that persona, and refreshes the list.
- `ThreadArchive.BuildSummary` trims the last visible user text and the last visible assistant text. Both empty becomes `Archived.` Otherwise they join with a newline when both exist. The result is cut at 2000 characters (`string.Length`, same check the Host uses).
- `SoulCorePersonaClient.ArchiveAsync` posts `ArchiveRequestJson` to `/api/personas/{personaId}/archive`. `ArchiveRequest_PostsSummaryObjectOnThePersonaRoute` passed: `{"summary":"Archived."}` and a newline escaped as `\n`.
- `ThreadHideMarks` stores the persona key and the latest `rowid` (0 when that transcript has no rows). `LoadRecent` keeps `rowid` greater than the mark. A persona with no mark stays listed, including an empty one.

## Commands

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter FullyQualifiedName~ThreadArchiveTests --logger "console;verbosity=detailed"
```

Branch during the run: `pr-177` (`git branch --show-current`).

The Windows profile folder is omitted from the paths below. The rest of the output is the command result.

### Build

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> (repo)\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> (repo)\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.72
```

### Tests

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Protocol -> (repo)\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> (repo)\House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll
  House.ChatDesktop.Tests -> (repo)\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
Test run for (repo)\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
(repo)\House\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.59]   Discovering: House.ChatDesktop.Tests
[xUnit.net 00:00:00.63]   Discovered:  House.ChatDesktop.Tests
[xUnit.net 00:00:00.63]   Starting:    House.ChatDesktop.Tests
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.OtherPersona_TranscriptIsUnchanged [326 ms]
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.ArchiveRequest_PostsSummaryObjectOnThePersonaRoute [48 ms]
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.FailedPost_DoesNotHideTheThread [61 ms]
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.LaterMessage_ShowsOnlyRowsAfterTheMark [93 ms]
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.SuccessfulPost_HidesRows_DropsThread_AndKeepsThemInTheDb [107 ms]
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.Summary_Trims_JoinsLastLines_AndCapsAt2000 [38 ms]
[xUnit.net 00:00:01.41]   Finished:    House.ChatDesktop.Tests
  Passed House.ChatDesktop.Tests.ThreadArchiveTests.EmptyExchange_SendsArchived_AndALaterMessageComesBack [81 ms]

Test Run Successful.
Total tests: 7
     Passed: 7
 Total time: 2.0406 Seconds
```
