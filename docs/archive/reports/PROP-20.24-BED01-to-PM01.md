---
type: report
prop_id: PROP-20.24
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
---

# PROP-20.24 — Operator resting weights Dominant 0.70 / Ambitious 0.80 / Uninhibited 0.80 — Pass

## Done

1. Baked operator resting weights into `VictoriaMindBlock.DefaultRestingTraits` per `prop-20-resting-seed-decision.md`.
2. Dominant **0.70 Mid** ledger `operator`; Ambitious + Uninhibited **0.80 High** ledger `operator`.
3. Possessive, Affectionate, Driven, Devoted, Intense, Proud remain **0.52 Mid** with `operator-provisional-until-seed`.
4. Resting block header now notes mixed ledger: `operator (set) / operator-provisional-until-seed (remaining)`.
5. Tests updated off the all-0.52 / same-ledger assumption; leisure name mirror still derives from defaults.

## Resting set (shipped)

| Name | Weight | Band | Ledger |
| --- | ---: | --- | --- |
| Dominant | **0.70** | Mid | operator |
| Possessive | 0.52 | Mid | operator-provisional-until-seed |
| Affectionate | 0.52 | Mid | operator-provisional-until-seed |
| Driven | 0.52 | Mid | operator-provisional-until-seed |
| Devoted | 0.52 | Mid | operator-provisional-until-seed |
| Intense | 0.52 | Mid | operator-provisional-until-seed |
| Uninhibited | **0.80** | High | operator |
| Ambitious | **0.80** | High | operator |
| Proud | 0.52 | Mid | operator-provisional-until-seed |

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Release \
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~Prop17LeisureTests" \
  -o .tmp\prop2024-test

Test Run Successful.
Total tests: 39
     Passed: 39
 Total time: 1.8555 Seconds
```

Key cases:

```
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights [6 ms]
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildContextPreamble_OrderIsIdentityMemoryEmotion [< 1 ms]
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_InjectsConstitutionFloorsOnly_NotCompassOrPersonality [1 ms]
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_Build_HasFloorsAndNineResting [< 1 ms]
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_UsesSharedVictoriaMindBlock [101 ms]
```

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | Dominant 0.70 Mid operator; Ambitious/Uninhibited 0.80 High operator; mixed ledger header |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | comment → PROP-20.24 (names still derived) |
| `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` | assert operator vs provisional split |
| `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` | preamble Contains for 0.70 Mid / 0.80 High |

## Acceptance

- [x] Dominant 0.70 Mid `operator`; Ambitious + Uninhibited 0.80 High `operator`
- [x] Other six remain 0.52 Mid provisional
- [x] Tests updated and green (output pasted above)
- [x] Report: `docs/agents/reports/PROP-20.24-BED01-to-PM01.md`

## Handoff

- Unblocks PROP-20.1d Release ALLSTART (remaining six weights still provisional until revised).
- Context-trait Host selection remains a later ticket.
