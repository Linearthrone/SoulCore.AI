---
type: task
prop_id: PROP-16.3b
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.3b-FED01-to-PM01.md
pm_note: Accepted 2026-10-22 as Pass (client). A failed post leaves the thread up. A successful post hides that persona's rows and keeps them in the database. 7 tests passed. The Archive button was not clicked in a running window.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.3 (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Hide an archived local thread
---

# PROP-16.3b — Archive hides the local thread

## Problem

The Host can store an archive summary (`POST /api/personas/{personaId}/archive`, body `{"summary":"..."}`). The chat drawer still lists that persona’s local transcript. Archive is supposed to hide the thread.

## Root

Local rows live in `presence-chat.db`, keyed by `ChatTranscriptKey.ForPersona`. The thread list is one row per persona (`PresenceThreadRow`). This ticket hides those rows. It does not delete them and it does not summarize with a model.

## Solution

1. Add an Archive action on a thread row.
2. Build the summary from the last user line and the last assistant line, trimmed. If both are empty, send `Archived.` Cap the posted text at 2000 characters.
3. Post that summary to `POST /api/personas/{personaId}/archive`. If the post fails, leave the thread visible and do not move the hide mark.
4. On success, record a hide mark for that persona key (the last row id, or a timestamp that means the same). Loads skip rows at or before the mark. The thread row is omitted when nothing newer is visible. Do not delete the rows.
5. A new message after the mark shows the thread again, and only the rows after the mark.

## Do not

- Call a model. Do not edit Host projects. BED owns `SoulCore/**` and is editing it on 16.4 in parallel; stay inside `House/House.ChatDesktop` and its tests.
- Trait sliders, description AI help, or chat-multi.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/Services/ChatHistoryStore.cs`
- `House/House.ChatDesktop/Services/ChatTranscriptKey.cs`
- `House/House.ChatDesktop/Models/PresenceThreadRow.cs`
- `House/House.ChatDesktop.Tests/`

## Acceptance

- [ ] A failed post does not hide the thread
- [ ] A successful post hides that persona’s rows and drops the thread row
- [ ] The hidden rows are still in `presence-chat.db`
- [ ] A later message on that persona shows only the new rows
- [ ] Another persona’s transcript is unchanged
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.3b-FED01-to-PM01.md`
