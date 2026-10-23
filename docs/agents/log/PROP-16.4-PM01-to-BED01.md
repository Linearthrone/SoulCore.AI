---
type: task
prop_id: PROP-16.4
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: BED-01
priority: P0
status: Pass
report: docs/agents/reports/PROP-16.4-BED01-to-PM01.md
pm_note: Accepted 2026-10-22. 31 tests passed. Eight-line cap holds, defaults emit nothing, description draft does not save the pack. Blank starters for formality and playfulness stay 0.4, so a blank pack still compiles those two lines. ImageSharp warnings in the log are existing package advisories.
created: 2026-10-22
updated: 2026-10-22
depends_on: PROP-16.3 (Pass)
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
routing: docs/agents/tasks/PROP-16.0-PM01-routing.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Trait catalog, eight-line compiler, description draft
---

# PROP-16.4 — Trait catalog and description draft

## Problem

`PersonaTraitScales` has six sliders, and `PersonaTraitCompiler` always emits a voice line, a boundary line, and a recall line. The accepted catalog is 30 traits on the creation page and 20 more on an advanced page. The prompt may carry at most eight directive lines. Traits left on their default stay out. A physical-description field and a local draft route do not exist. The draft must not save the pack.

## Root

Scales and the compiler live in `SoulCore/SoulCore.Core/Persona/PersonaPack.cs` and `PersonaTraitCompiler.cs`. Pack JSON is the store. Missing keys take the property default, so changing a default changes every old pack that omitted the field.

## Solution

1. Keep the six existing property names and their current defaults (all `0.5` today). Add the other catalog traits as 0–1 properties. Use the proposal’s Top 30 and Top 50 defaults for the new ones. Top 50 is the same object, not a second list. Old JSON that lacks a new key loads that trait at its default.
2. Replace the compiler output. Snap with the existing edges (under 0.34, under 0.67, else). Emit at most eight directive lines after the `[Persona directives]` marker. The marker is not one of the eight. If every trait is on its default, emit no marker and no lines.
3. A trait on its default emits nothing. Off-default traits are ordered by absolute distance from that trait’s own default, ties broken by the catalog order in the proposal. Boundary strictness, when it is off default, is the first line and counts toward the eight.
4. Line text describes manner only. It must not say “obey”, and compiling or saving traits must not change tool flags. When the blurb contains `## LIMITS`, the compiled block must not tell the model to loosen those limits. Keep the PROP-15.17 rule that a low boundary line does not undercut LIMITS.
5. Add `PhysicalDescription` on the pack, missing JSON means empty. Reject a pack save whose trimmed description is longer than 1000 characters.
6. Add `POST /api/personas/{personaId}/description-draft`. Call `CompleteAsync` only (no tool loop), using the pack model when it is set, otherwise the Host model. Return the text, truncated to 1000 characters. Do not write the pack, charter, episodic store, or tool flags. If the model is down, return 503 and write nothing.

## Do not

- Avalonia. FED is editing `House/House.ChatDesktop` on 16.3b in parallel. Stay inside `SoulCore/**` and the protocol tests.
- Chat-multi, archive UI, or a cloud rewrite.
- Inject all 30 or all 50 traits into the prompt.
- Commit or push unless the operator asks.

## Files

- `SoulCore/SoulCore.Core/Persona/PersonaPack.cs`
- `SoulCore/SoulCore.Core/Persona/PersonaTraitCompiler.cs`
- `SoulCore/SoulCore.Host/Persona/PersonaApiEndpoints.cs`
- `SoulCore/SoulCore.Protocol.Tests/` trait and pack-merge tests

## Acceptance

- [ ] An old pack JSON without the new keys still loads, and the original six stay at their stored values
- [ ] A trait moved across 0.34 or 0.67 changes the compiled line; a trait left on default adds no line
- [ ] Fifty traits set far from default still produce at most eight directive lines
- [ ] A blurb that contains `## LIMITS` is unchanged by compile, and the block does not grant tools or say “obey”
- [ ] Description draft returns text and leaves pack bytes, charter, and episodic rows unchanged
- [ ] Model-down returns 503 and writes nothing. A description over 1000 characters is rejected on save
- [ ] `dotnet test` for the new and updated trait tests passes, with the output in the report
- [ ] Report lists commands and results

## Report

`docs/agents/reports/PROP-16.4-BED01-to-PM01.md`
