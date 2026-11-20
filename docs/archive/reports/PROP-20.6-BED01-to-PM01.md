---
type: report
prop_id: PROP-20.6
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
title: Leisure mind gate + desk-model fallback
---

# PROP-20.6 — BED-01 completion

**Verdict: Pass**

## What shipped

1. **Mind block on leisure** — `LeisureMindBlock` builds floors + resting via shared `VictoriaMindBlock` (PROP-20.4), then appends `[Interest reason]` + related memories. Channel text stays in `LeisurePrompt` beside the block.
2. **Write / speak-as-her gate** — `LeisureRunner` suppresses journal notes, find broadcasts, and suspended proposals unless `LeisureMindBlock.MayWriteAsHer(system)` (floors header + all seven resting names).
3. **Desk chat model fallback** — If the mind block exceeds `SmallLeisureMindBudgetChars` (4500), that pass uses `InferenceModelRouting.ResolveChatModel` via injected desk `IInferenceClient`, with mind block still present. Empty `LeisureModel` still means quiet (no inventing a leisure model).
4. **LeisureTools deny list** — Unchanged allow/deny; comment notes PROP-20.6; test asserts `browser_navigate` denied.

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | New gate + builder (prefers 20.4 `VictoriaMindBlock`) |
| `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs` | Inject mind block; model select; write gate; desk fallback |
| `SoulCore/SoulCore.Inference/Presence/LeisureTools.cs` | Comment only |
| `SoulCore/SoulCore.Protocol.Tests/Prop17LeisureTests.cs` | Mind / write-suppress / fallback / shared-builder tests |

**Not touched:** SMS Companion logic, journal migrations, ChatDesktop.

## Acceptance

| Criterion | Result |
| --- | --- |
| Write-as-her absent unless prompt had floors + resting | **Pass** — `MayWriteAsHer` / runner suppress paths |
| Small-model failure → desk chat model with mind block still present | **Pass** — `ResolvePassModel` + oversized-block test |
| LeisureTools deny list intact | **Pass** |
| Tests + this report | **Pass** |

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~Prop17LeisureTests"
Passed!  - Failed: 0, Passed: 22, Skipped: 0, Total: 22
```

New coverage:

- `LeisureMindBlock_Temporary_HasFloorsAndSevenResting`
- `LeisureMindBlock_WriteSuppressed_WithoutFloorsOrResting`
- `LeisureMindBlock_SmallModelFailure_SelectsDeskChatModel`
- `LeisureMindBlock_UsesSharedVictoriaMindBlock`
- `LeisureMindBlock_SharedBuilderHook_PreferredWhenValid`

## Notes for PM

- Soft-depend on 20.4 satisfied: leisure calls `VictoriaMindBlock.Build` for floors+resting; seed safety/value floors used when charter floors are not injected into the leisure path.
- Provisional small-model budget is char-based (`4500`); tune later if live 1.5b context probes are needed.
- Desk fallback requires registered `IInferenceClient`; if missing when over budget, pass stays quiet (no blank stand-in).
