---
type: task
prop_id: PROP-16.6
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: BED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.6-BED01-to-PM01.md
pm_note: Accepted 2026-10-22. ChatMultiTests 6 passed. Mode defaults to one. A background turn writes that persona's store and does not dispatch a tool. A foreground change cancels the previous tool loop. ImageSharp and CA1416 warnings in the log are existing. The One/Many chip is 16.6b.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.5 (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Chat-multi — several chats, one tool owner
---

# PROP-16.6 — Chat-multi

## Problem

A chat turn uses whichever pack `IPersonaSession.GetActive()` returns, and `chat.send` carries only `text` and an optional `sessionId`. Tool policy on the pack is prompt text. Dispatch still offers the whole registry from `ChatSendHandler.CompleteChatWithToolsAsync`. Many mode has to let several personas chat, each against their own store, while only the foreground persona may use tools, the VM, or SoulLoop.

## Root

`ChatContextBuilder` and `ChatSendHandler` read the active session at turn time. `IPersonaStoreHub.GetMemoryStore` can already open a named persona without switching the foreground. The tool loop calls `_toolRegistry.GetDefinitions()` with no pack filter.

## Solution

1. Add a Host mode, default `one`, missing store means `one`. `GET /api/personas/mode` and `POST /api/personas/mode` with `{"mode":"one"}` or `{"mode":"many"}`. Register those routes before `/{personaId}`. Any other value is 400.
2. `chat.send` may include `personaId`. Before any await, copy the mode, the foreground id, and the payload `personaId` into locals. Do not read `GetActive()` again later in that turn for the pin.
3. Mode `one`: a missing `personaId` means the foreground. A `personaId` that is not the foreground is rejected and writes nothing. Mode `many`: `personaId` is required. Context, transcript, and the episodic write use that persona’s pack and `GetMemoryStore(personaId)`. Leave the foreground where it was.
4. Tools, the VM, browser, email, and SoulLoop run only when the pinned id is the foreground. A background turn uses `CompleteAsync` only. Do not call `CompleteWithToolsAsync` for it.
5. On a foreground turn, filter the registry with that pack’s `ToolPolicy` before the loop. `AllowDesktop` false drops desktop tools. `AllowBrowser` false drops browser tools. `AllowEmail` false drops email tools. A refused tool is not dispatched.
6. Changing the foreground cancels an in-flight tool loop that was pinned to the previous foreground. A background chat with no tools is not cancelled by that change.
7. One Host process. No VM per background persona. SoulLoop keeps writing the foreground bundle only.

## Do not

- Avalonia, the settings chip, or the radial. Those wait for a later FED ticket.
- A second Host process, or N SoulLoop instances.
- Commit or push unless the operator asks.

## Files

- `SoulCore/SoulCore.Protocol/SoulCoreFrame.cs`
- `SoulCore/SoulCore.Host/Ws/ChatSendHandler.cs`
- `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs`
- `SoulCore/SoulCore.Host/Persona/PersonaApiEndpoints.cs`
- `SoulCore/SoulCore.Host/Loop/SoulLoopScaffold.cs`
- `SoulCore/SoulCore.Protocol.Tests/`

## Acceptance

- [ ] Missing mode storage reads as `one`
- [ ] Mode `one` writes the foreground store when `personaId` is omitted, and rejects another persona without writing their store
- [ ] Mode `many` writes persona B’s episodic store while A stays foreground, and that turn does not dispatch a tool
- [ ] A foreground turn with `AllowDesktop` false does not dispatch a desktop tool
- [ ] A foreground change cancels the previous foreground’s in-flight tool loop
- [ ] SoulLoop does not write a background persona’s store
- [ ] `dotnet test` for the new tests passes, with the output in the report
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.6-BED01-to-PM01.md`
