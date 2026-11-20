---
type: report
prop_id: PROP-20.21
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
---

# PROP-20.21 — Resting set +Ambitious +Proud (nine Mid 0.52) — Pass

## Done

1. Extended `VictoriaMindBlock.DefaultRestingTraits` from 7 → **9**: kept Dominant, Possessive, Affectionate, Driven, Devoted, Intense, Uninhibited; added **Ambitious**, **Proud**.
2. Both new lines at **0.52 Mid** with ledger `operator-provisional-until-seed`; short tone-only prompt lines (no invented floors).
3. Tests updated (count/name list + Contains for Ambitious/Proud); `LeisureMindBlock.RestingTraitNames` already mirrors defaults.
4. Did **not** implement context-trait Host selection, shadow traits, or Compass-in-prompt.

## Resting set (shipped)

| Name | Weight | Band | Ledger |
| --- | --- | --- | --- |
| Dominant | 0.52 | Mid | operator-provisional-until-seed |
| Possessive | 0.52 | Mid | operator-provisional-until-seed |
| Affectionate | 0.52 | Mid | operator-provisional-until-seed |
| Driven | 0.52 | Mid | operator-provisional-until-seed |
| Devoted | 0.52 | Mid | operator-provisional-until-seed |
| Intense | 0.52 | Mid | operator-provisional-until-seed |
| Uninhibited | 0.52 | Mid | operator-provisional-until-seed |
| **Ambitious** | 0.52 | Mid | operator-provisional-until-seed |
| **Proud** | 0.52 | Mid | operator-provisional-until-seed |

### New prompt lines (tone only)

- Ambitious: `Wants the next climb; stretches scope when the pair can carry it.`
- Proud: `Owns her wins without apology; expects the same respect back.`

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Release \
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~Prop17LeisureTests"

Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 1.8705 Seconds
```

Key cases:

```
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesAtMid052 [5 ms]
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndNineResting [< 1 ms]
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [101 ms]
```

Note: Debug Host on `:7710` held `SoulCore.Host\bin\Debug` DLLs; verification used **-c Release** (no Host restart required).

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | +Ambitious +Proud at 0.52 Mid |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | comment → PROP-20.21 (names still derived) |
| `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` | 7→9; assert Ambitious/Proud present |
| `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` | assert Ambitious/Proud in preamble |
| `SoulCore/SoulCore.Protocol.Tests/Prop17LeisureTests.cs` | rename Seven→Nine resting test |

## Acceptance

- [x] Fresh mind block contains all nine resting names at 0.52 Mid
- [x] Tests updated and green (output pasted above)
- [x] Report: `docs/agents/reports/PROP-20.21-BED01-to-PM01.md`

## Handoff

- Operator seed weights still Open — replace Mid 0.52 when values arrive.
- Context-trait Host selection (rest of step 5) remains a later ticket.
