---
type: report
prop_id: PROP-16.5
prop_root: PROP-16-presence-glass-overlay
from: FED-01
to: PM-01
priority: P0
status: Ready for QA-01
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.5-PM01-to-FED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.5 — Creation and contacts — FED verification Pass

## Verdict

**Pass** on the FED checks below. Ready for QA-01. No commit. No push. Branch stayed `pr-177`. No files under `SoulCore/` were edited. Chat-multi was not started.

The desktop project references `SoulCore.Core`. Sliders, the save map, and load defaults all walk `PersonaTraitCatalog.All`. The fifty trait names are not copied into the client. The original six properties on `PersonaTraitScalesInfo` stay so existing wizard tests still compile.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| The wizard shows the five creation groups one at a time, then More traits for the last 20 | Pass | `TraitGroups_follow_catalog_order_then_more_traits` and `WizardSteps_walk_template_identity_model_groups_blurb_quarantine`. Order is Voice 6, Judgment 6, Thinking and drive 9, With people 6, With other AIs 3, then More traits 20. Slices match catalog order. `ShowsTraitGroup` keeps More traits hidden until that step is the open one. The window builds one slider per catalog trait and shows only the open group's panel. Steps stay template, identity and portrait, model, trait groups, blurb, quarantine confirm. |
| A save payload contains all 50 traits, and a trait left untouched is that trait’s catalog default | Pass | `SaveMap_includes_every_catalog_trait_at_its_default_when_untouched` and `SavePayload_sends_all_50_and_keeps_blank_starter_formality_and_playfulness`. The JSON `traits` object has 50 keys. An untouched map value equals `PersonaTraitDefinition.Default` (metaphor use is not 0.5). Blank-starter formality and playfulness stay 0.4. |
| Loading a pack maps every stored trait onto a slider; a missing key shows the catalog default | Pass | `LoadTraits_missing_key_uses_catalog_default_and_keeps_stored_values` and `EditWrite_reloads_every_trait_and_the_description`. `{"warmth":0.2,"formality":0.4}` keeps those two and leaves every other trait on its catalog default. Edit write still carries warmth 0.81 and adaptability 0.12, and the other keys. The window `ApplyTraits` writes each catalog value onto the matching slider. |
| AI help writes only the description box, and only after a successful draft response | Pass | `DescriptionBox_caps_at_1000_and_draft_writes_only_that_box`. A failed draft leaves the description at "old face". A successful draft replaces it, capped at 1000. The button calls `POST /api/personas/{personaId}/description-draft` with body `{}` only when the persona is already on the Host. |
| AI help does not change the blurb, LIMITS, or tool flags, and a missing persona does not create a pack | Pass | Same test. Blurb stays `charter ## LIMITS stay`. `AllowDesktop` / `AllowBrowser` / `AllowEmail` stay false / true / false. `DecideDescriptionDraft(false)` sets `CallRoute` false and `CreatePack` false, and the status says the persona must already be saved. The draft request body has no `identityBlurb`, `toolPolicy`, `traits`, or `LIMITS`. The window returns before any HTTP call in that case, and the draft method does not call create or update. |
| Contacts shows the description and Edit reloads the full set | Pass | `Contacts_show_saved_physical_description_under_the_name`. The contacts detail stack is portrait, name, description, id, quarantine line, then Open chat, Edit, Pin, Set photo. Empty description is hidden. Edit still reloads the pack with `GetAsync` before the wizard; `FromDto` now keeps `physicalDescription` and the full scales object, and the wizard applies both. |
| `dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj` succeeds | Pass | Build succeeded. 0 warnings, 0 errors. Output below. |
| Report lists commands and results | Pass | This file. |

## What changed

- `House.ChatDesktop.csproj` references `SoulCore.Core`.
- Wizard steps: template, identity and portrait (physical description, 1000 characters, AI help), model, one trait group at a time, blurb, quarantine confirm.
- A slider on the catalog default shows a Default badge. Save JSON includes every catalog trait. Missing JSON keys load at the catalog default. Template starter formality and playfulness stay 0.4.
- AI help fills only the physical-description box after a successful draft. It does not write the pack.
- Contacts shows the saved physical description under the name. Open chat, Edit, Pin, Set photo, and the quarantine line stay.

## Commands

Branch during the runs: `pr-177` (`git branch --show-current`).

Paths below are relative to the repo root. The runner printed them under that root.

### Build

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --nologo
```

```text
  Determining projects to restore...
  Restored House/House.ChatDesktop/House.ChatDesktop.csproj (in 485 ms).
  2 of 3 projects are up-to-date for restore.
  SoulCore.Core -> SoulCore/SoulCore.Core/bin/Debug/net8.0/SoulCore.Core.dll
  SoulCore.Protocol -> SoulCore/SoulCore.Protocol/bin/Debug/net8.0/SoulCore.Protocol.dll
  House.ChatDesktop -> House/House.ChatDesktop/bin/Debug/net8.0/House.ChatDesktop.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:07.91
```

### Save payload, description box, and group split

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~PersonaCatalogWizardTests" --logger "console;verbosity=detailed"
```

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  SoulCore.Core -> SoulCore/SoulCore.Core/bin/Debug/net8.0/SoulCore.Core.dll
  SoulCore.Protocol -> SoulCore/SoulCore.Protocol/bin/Debug/net8.0/SoulCore.Protocol.dll
  House.ChatDesktop -> House/House.ChatDesktop/bin/Debug/net8.0/House.ChatDesktop.dll
  House.ChatDesktop.Tests -> House/House.ChatDesktop.Tests/bin/Debug/net8.0/House.ChatDesktop.Tests.dll
Test run for House/House.ChatDesktop.Tests/bin/Debug/net8.0/House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
House/House.ChatDesktop.Tests/bin/Debug/net8.0/House.ChatDesktop.Tests.dll
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.5.3.1+6b60a9e56a (64-bit .NET 8.0.31)
[xUnit.net 00:00:00.04]   Discovering: House.ChatDesktop.Tests
[xUnit.net 00:00:00.07]   Discovered:  House.ChatDesktop.Tests
[xUnit.net 00:00:00.07]   Starting:    House.ChatDesktop.Tests
[xUnit.net 00:00:00.16]   Finished:    House.ChatDesktop.Tests
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.DescriptionBox_caps_at_1000_and_draft_writes_only_that_box [3 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.LoadTraits_missing_key_uses_catalog_default_and_keeps_stored_values [35 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.SavePayload_sends_all_50_and_keeps_blank_starter_formality_and_playfulness [9 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.Contacts_show_saved_physical_description_under_the_name [< 1 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.SaveMap_includes_every_catalog_trait_at_its_default_when_untouched [< 1 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.WizardSteps_walk_template_identity_model_groups_blurb_quarantine [5 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.EditWrite_reloads_every_trait_and_the_description [< 1 ms]
  Passed House.ChatDesktop.Tests.PersonaCatalogWizardTests.TraitGroups_follow_catalog_order_then_more_traits [1 ms]

Test Run Successful.
Total tests: 8
     Passed: 8
 Total time: 0.5183 Seconds
```

### Existing wizard logic (regression)

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --nologo --filter "FullyQualifiedName~PersonaCatalogWizardTests|FullyQualifiedName~PersonaWizardLogicTests"
```

```text
Passed!  - Failed:     0, Passed:    30, Skipped:     0, Total:    30, Duration: 112 ms - House.ChatDesktop.Tests.dll (net8.0)
```
