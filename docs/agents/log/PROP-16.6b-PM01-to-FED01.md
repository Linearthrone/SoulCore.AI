---
type: task
prop_id: PROP-16.6b
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass
report: docs/agents/log/PROP-16.6b-FED01-to-PM01.md
pm_note: Accepted 2026-10-22 as Pass (client). PersonaModeTests 8 passed. One sends only the foreground. Many sends the open thread and does not activate it. The controls were not clicked in a running window. This closes the ticketed chain.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.6 (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: One/Many chip and pinned chat send
---

# PROP-16.6b — One/Many chip

## Problem

The Host now has `GET` and `POST /api/personas/mode` (`one` or `many`, default `one`) and `chat.send` accepts `personaId`. Presence still posts `chat.send` with `sessionId` `presence-local` and no `personaId` (`House/House.ChatDesktop/Services/SoulCoreWsClient.cs`). There is no One/Many control. The radial frame’s MANY chip has no matching control.

## Root

Mode and the pin live on the Host. The desktop never reads them. A send without `personaId` is a foreground turn, so a background thread cannot chat.

## Solution

1. Add one One/Many control on the radial, and the same control in Settings → System. Both show the value from `GET /api/personas/mode`. Until that GET returns, show One. Choosing a value `POST`s `{"mode":"one"}` or `{"mode":"many"}`. A failed post puts the control back.
2. `SendChatAsync` includes `personaId` for the thread being sent. In One, that id is the foreground persona. If the open thread is not the foreground, do not send. In Many, send the open thread’s id and do not activate that persona.
3. Focusing a thread may still activate it, as it does today. Sending does not.
4. The HOUSE drawer and the viewport still name the foreground persona. Focusing a background thread in Many does not rename them.

## Do not

- Edit `SoulCore/**`.
- A second Host, or a VM per persona.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/Services/SoulCoreWsClient.cs`
- `House/House.ChatDesktop/Controls/LeftEdgeRadial.cs`
- `House/House.ChatDesktop/Drawers/RadialEdgeWindow.cs`
- Settings System section in `House/House.ChatDesktop/`
- `House/House.ChatDesktop.Tests/`

## Acceptance

- [ ] The radial and Settings show the same mode, and a missing GET displays One
- [ ] A failed POST leaves the previous mode on both controls
- [ ] One mode sends the foreground `personaId`, and does not send a different open thread
- [ ] Many mode sends the open thread’s `personaId` and does not activate that persona
- [ ] HOUSE and the viewport still name the foreground persona after a background thread is focused
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.6b-FED01-to-PM01.md`
