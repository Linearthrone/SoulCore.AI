---
type: task
prop_id: PROP-16.5
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: FED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.5-FED01-to-PM01.md
pm_note: Accepted 2026-10-22 as Pass (client). Catalog tests 8 passed; wizard regression 30 passed. Save sends all 50 traits. AI help fills only the description box. The wizard was not clicked in a running window. Chat-multi was not started.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.4 (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Bind creation and contacts to the trait catalog
---

# PROP-16.5 — Creation and contacts

## Problem

The wizard still shows six sliders (`PersonaWizardWindow.axaml`). The Host catalog is fifty traits on one object (`SoulCore/SoulCore.Core/Persona/PersonaTraitCatalog.cs`), and the pack now has `physicalDescription`. The desktop DTO `PersonaTraitScalesInfo` still has only the original six, so a save would drop the rest. Contacts does not show the description.

## Root

`House.ChatDesktop` references `SoulCore.Protocol` only. The catalog lives in `SoulCore.Core`, which is a small library. The client must read that catalog rather than keep a second list of names.

## Solution

1. Reference `SoulCore.Core` from `House.ChatDesktop`. Drive the sliders from `PersonaTraitCatalog.All` in catalog order. Do not copy the fifty names into the client.
2. The wizard still walks template, identity and portrait, model, trait groups, blurb, quarantine confirm. Show one trait group at a time. Groups, by catalog order: Voice (first 6), Judgment (next 6), Thinking and drive (next 9), With people (next 6), With other AIs (next 3). The last 20 are behind a More traits step, hidden until that step is opened.
3. A slider sitting on `PersonaTraitDefinition.Default` shows a Default badge. A skipped trait is saved at that default, not omitted. The save body includes every catalog trait. A missing key on load displays the catalog default. Do not change the template starter numbers (formality and playfulness on a blank starter stay 0.4).
4. Add a physical-description box, separate from `IdentityBlurb`, capped at 1000 characters. An AI help button calls `POST /api/personas/{personaId}/description-draft` and, on success, fills only that box. The operator saves. The button does not write the pack, the blurb, LIMITS, or tool flags. If this persona is not on the Host yet, do not call the route and do not create a pack; leave the box unchanged and say the persona must already be saved.
5. Contacts keeps Open chat, Edit, Pin, Set photo, and the quarantine line. Under the name, show the saved physical description. Edit opens this wizard with every trait and the description loaded.

## Do not

- Edit `SoulCore/**` except the new project reference from the desktop csproj. Do not change the compiler.
- Chat-multi.
- A model call from the client other than the description-draft route.
- Commit or push unless the operator asks.

## Files

- `House/House.ChatDesktop/House.ChatDesktop.csproj`
- `House/House.ChatDesktop/PersonaWizardWindow.axaml`
- `House/House.ChatDesktop/PersonaWizardWindow.axaml.cs`
- `House/House.ChatDesktop/Services/PersonaWizardLogic.cs`
- `House/House.ChatDesktop/Services/SoulCorePersonaClient.cs`
- `House/House.ChatDesktop/Drawers/ContactsDrawerWindow.cs`
- `House/House.ChatDesktop.Tests/`

## Acceptance

- [ ] The wizard shows the five creation groups one at a time, then More traits for the last 20
- [ ] A save payload contains all 50 traits, and a trait left untouched is that trait’s catalog default
- [ ] Loading a pack maps every stored trait onto a slider; a missing key shows the catalog default
- [ ] AI help writes only the description box, and only after a successful draft response
- [ ] AI help does not change the blurb, LIMITS, or tool flags, and a missing persona does not create a pack
- [ ] Contacts shows the description and Edit reloads the full set
- [ ] `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.5-FED01-to-PM01.md`
