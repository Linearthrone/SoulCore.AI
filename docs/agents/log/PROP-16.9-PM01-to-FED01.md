---
type: task
prop_id: PROP-16.9
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass (client)
created: 2026-10-23
updated: 2026-10-23
depends_on: PROP-16.8b (Pass)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Trait hover bubbles and a chat drawer that opens
---

# PROP-16.9 — Trait hovers and Open chat

## Problem

LinearThrone wants a hover bubble on every trait slider. It should say what the slider changes, plus an example of behavior at the low end and at the high end.

Opening a chat does not show the drawer. Contacts **Open chat** calls `SwitchToPersonaAsync`. When that persona is already foreground, the method returns before `ShowChatDrawer`. The radial window is `Topmost`, so a chat window that does `Show` can stay behind it.

## Root

Slider rows are built in `House/House.ChatDesktop/PersonaWizardWindow.axaml.cs` from `PersonaTraitCatalog.All`. Nothing sets a tooltip. `MainWindow.Persona.cs` `SwitchToPersonaAsync` returns immediately on the active id. `OpenPersonaFromContactsAsync` does not show the drawer after that return. `RadialEdgeWindow` sets `Topmost = true`.

## Solution

1. Every catalog trait gets hover text: one sentence on what the slider changes, then a low-end example and a high-end example. Put the strings on the catalog row so the wizard does not keep a second list of names. The compiler lines stay as they are. This text is for the operator, not the prompt.
2. The wizard sets that bubble on the slider and its label, on every group including More traits.
3. **Open chat** always shows the chat drawer, including when the persona is already foreground. Do not activate again in that case. If the operator cancels the quarantine confirm, do not switch and do not open.
4. The chat drawer comes in front of the radial and the VM tab. If the window is off the work area, move it back on screen before showing it.

## Do not

- Change band edges, the eight-line cap, or tool policy.
- Commit or push.
- Stop the Host on 7720 or the Soul_Core Host on 7700.

## Files

- `SoulCore/SoulCore.Core/Persona/PersonaTraitCatalog.cs`
- `House/House.ChatDesktop/PersonaWizardWindow.axaml.cs`
- `House/House.ChatDesktop/MainWindow.Persona.cs`
- `House/House.ChatDesktop/MainWindow.Glass.cs`
- `House/House.ChatDesktop/Drawers/RadialEdgeWindow.cs`
- `House/House.ChatDesktop.Tests/`

## Acceptance

- [ ] All 50 traits have a hover with a description, a low example, and a high example
- [ ] The bubble is on the slider and the label in every wizard group
- [ ] Open chat on the already-foreground persona shows the drawer and does not call activate
- [ ] A cancelled quarantine confirm does not show the drawer and does not switch
- [ ] The chat drawer is in front of the radial and the VM tab
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.9-FED01-to-PM01.md`
