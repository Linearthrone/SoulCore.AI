---
type: task
prop_id: PROP-16.3
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: BED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.3-BED01-to-PM01.md
pm_note: Accepted 2026-10-22. PersonaArchiveTests 5 passed. archive is not rewritten to system and is not quarantined. The write uses the named persona's store while another persona stays active. ImageSharp NU1902/NU1903 warnings in that run are pre-existing package advisories, not this ticket.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.2b (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Archive summary into that persona's episodic store
---

# PROP-16.3 — Archive write

## Problem

Archive is supposed to write a short summary into that persona’s episodic store and nowhere else. `MemorySourceNormalizer` allows `self`, `chat`, `imported`, `observation`, `correction`, `system`, and `model`. The label `archive` is rewritten to `system`. There is no persona-scoped archive route. Live chat writes go through the active facade (`PersonaScopedMemoryStore`), so a write issued while another persona is active would land in the wrong file.

## Root

Episodic rows are per persona bundle (`IPersonaStoreHub.GetMemoryStore`). The source allow-list drops unknown labels. This ticket is the write only. Hiding the thread in the chat drawer is a later FED ticket.

## Solution

1. Allow `archive` as its own source. Do not collapse it to `system`. Do not mark it quarantined (that flag is for `imported`).
2. Add `POST /api/personas/{personaId}/archive` on the existing persona group in `SoulCore/SoulCore.Host/Persona/PersonaApiEndpoints.cs`. Body is the summary text the caller already has. This ticket does not call a model to invent the summary.
3. Resolve the store with `GetMemoryStore(personaId)` for that id, including when a different persona is active. Unknown id returns 404. Blank text returns 400. Text longer than 2000 characters after trim returns 400.
4. Persist with `WriteEpisodicAsync(summary, "archive")` on that store only. Do not write charter, LIMITS, tool flags, or any other persona’s database.

## Do not

- Avalonia, the chat drawer, or hiding a local transcript.
- A model call, tool loop, or cloud rewrite.
- Trait schema or chat-multi.
- Commit or push unless the operator asks.

## Files

- `SoulCore/SoulCore.Memory/MemorySourceNormalizer.cs`
- `SoulCore/SoulCore.Host/Persona/PersonaApiEndpoints.cs`
- `SoulCore/SoulCore.Host/Persona/PersonaScopedStoreFacades.cs` (read; use the hub, not the active facade)
- Tests beside `SoulCore/SoulCore.Protocol.Tests/PersonaStoreHubQuarantineTests.cs`

## Acceptance

- [ ] A row written for persona A with source `archive` is stored as `archive`, not `system`
- [ ] That row is readable from A’s store and absent from persona B’s store
- [ ] The write still hits A when B is the active persona
- [ ] Unknown persona is 404. Blank and over-2000-character summaries are 400
- [ ] Charter, LIMITS, and tool flags are unchanged
- [ ] `dotnet test` for the new tests passes, and the command output is in the report
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.3-BED01-to-PM01.md`
