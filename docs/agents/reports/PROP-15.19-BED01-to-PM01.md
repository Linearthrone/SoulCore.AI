---
type: report
prop_id: PROP-15.19
prop_root: PROP-15-persona-creation-platform
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.17b (Pass)
ops_report: docs/agents/reports/PROP-15.17b-OPSHOME-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: PUT /api/personas/{id} merges sparse body onto existing pack
---

# PROP-15.19 — Partial PUT merge — **Pass**

## Verdict

**Pass.** `PUT /api/personas/{id}` now loads the existing pack, applies only properties present in the JSON body, and persists the merge. Sparse `{ "inferenceModel": "…" }` no longer wipes `IdentityBlurb`, traits, displayName, or titles. Empty / metadata-only bodies are rejected with 400.

## Root cause (15.17b)

PUT deserialized the body into a full `PersonaPack`. Omitted properties became C# defaults (`DisplayName=Blank`, null blurb, 0.5 traits), then `UpsertAsync` overwrote `pack.json`.

## Fix

1. **`PersonaPackMerge.TryApplySparsePatch`** — clone existing pack; apply case-insensitive present properties only; nested merge for `traits` / `toolPolicy`; reject `{}`, personaId-only, and mismatched path/body `personaId`.
2. **`PersonaApiEndpoints` PUT** — parse body as `JsonElement`, merge onto `GetAsync`, then `UpsertAsync` merged pack (still reloads active session when editing the active pack).

Explicit `null` on optional strings (e.g. `"inferenceModel": null`) clears that field; omission leaves it unchanged.

## Files changed

### Added
- `SoulCore/SoulCore.Host/Persona/PersonaPackMerge.cs`
- `SoulCore/SoulCore.Protocol.Tests/PersonaPackMergeTests.cs` (7 tests)

### Modified
- `SoulCore/SoulCore.Host/Persona/PersonaApiEndpoints.cs` (PUT merge path)

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~PersonaPackMergeTests" -c Release

Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 145 ms
```

Coverage:
- Sparse `inferenceModel` keeps IdentityBlurb / traits / titles / displayName
- Sparse nested `traits.warmth` keeps other scales + blurb
- Empty `{}` and personaId-only rejected
- Mismatched personaId rejected
- Explicit null clears model only
- Store round-trip (upsert → merge → upsert → get) preserves pack

## Ops note

Release Host **PID 33664** was stopped so DLLs could rebuild (file lock). **OPS-HOME** should recycle Release Host on `:7700` to pick up this binary before QA/ops pins again. Sparse model pins via PUT are now safe.

## Ask PM

1. Accept PROP-15.19; archive when ready.
2. Dispatch OPS-HOME Host recycle (post-15.19 Release).
3. Optional: re-allow sparse PUT for model pin in ops runbooks (was banned in 15.17b).
