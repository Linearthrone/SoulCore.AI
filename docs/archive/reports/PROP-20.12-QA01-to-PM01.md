---
type: report
prop_id: PROP-20.12
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
depends_on: PROP-20.11
title: Regression after PROP-20.11 slop cleanup
---

# PROP-20.12 — QA-01 post-slop regression → PM-01

**Verdict: Pass**

Targeted suites after PROP-20.11 Wave A cleanup: **121/121 Passed**, Failed 0. No issues filed.

**Ready for SLOP-01** (post-QA slop audit).

## Scope

| Area | Filter / check | Result |
| --- | --- | --- |
| SMS | `SmsSecurityGate`, `SmsOutboundComposer`, `SmsInbound`, `SmsOutbound` | **Pass** |
| Leisure | `Prop17LeisureTests` | **Pass** |
| Mind / memory | `VictoriaMindBlockTests`, `ChatContextBuilderTests`, `MemoryRecallDatedTests` | **Pass** |
| F1 confirm | No `SmsTools` callers left | **Pass** (rg: 0 matches under `SoulCore/`) |
| F7 confirm | Bare `[Resting]` does not pass write-as-her gate | **Pass** (`LeisureMindBlock_WriteSuppressed_WithoutFloorsOrResting`) |
| F2/F3 confirm | Truncate / strip still behave | **Pass** (composer truncate + untrusted strip suites green; callers use `TruncateForCarrier` / `StripWhereMarked`) |

## Command

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Debug --filter "FullyQualifiedName~SmsOutboundComposer|FullyQualifiedName~SmsOutbound|FullyQualifiedName~SmsInbound|FullyQualifiedName~SmsSecurityGate|FullyQualifiedName~Prop17Leisure|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~MemoryRecallDated" --logger "console;verbosity=normal"
```

## Result (pasted output)

```text
Test Run Successful.
Total tests: 121
     Passed: 121
 Total time: 1.2943 Seconds
```

Exit code: **0**. Representative confirmations from the same run:

```text
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_WriteSuppressed_WithoutFloorsOrResting [< 1 ms]
  Passed SoulCore.Protocol.Tests.Prop17LeisureTests.UntrustedObservation_CannotPoisonPreamble [3 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.Compose_LongAnswer_TruncatesAndFlagsPaper [4 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.Compose_480HardCeiling [< 1 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.RecallRecentDatedAsync_ReturnsOccurredAtWithContent [492 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.MemoryTape_OrderTodayFirst_TwoDatedEpisodes [< 1 ms]
  Passed SoulCore.Protocol.Tests.MemoryRecallDatedTests.RecallSimilarDatedAsync_ReturnsOccurredAtWithContent [263 ms]
```

## Static confirms (non-test)

| Check | Evidence |
| --- | --- |
| No `SmsTools` | `Grep` over `SoulCore/` → **no matches** (alias class gone; no callers) |
| Bare `[Resting]` rejected | Test asserts `MayWriteAsHer(bareResting) == false` when only literal `[Resting]` present; gate requires `VictoriaMindBlock.RestingMarker` (`[Resting traits]`) |
| Truncate path | Product callers use `SmsOutboundComposer.TruncateForCarrier` (inbound/outbound); twin helpers absent per PROP-20.11 |
| Strip path | `MemoryTape.StripUntrusted` → `UntrustedObservation.StripWhereMarked`; `StripFromPreamble` delegates same helper |

## Issues

None. No Fail; no `docs/agents/issues/` file opened for this PROP.

## Handoff

PROP-20.11 cleanup holds under QA regression. PM-01: accept / archive chain as appropriate; dispatch **SLOP-01** next.
