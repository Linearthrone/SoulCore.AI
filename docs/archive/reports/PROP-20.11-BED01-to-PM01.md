---
type: report
prop_id: PROP-20.11
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.10
slop_report: docs/agents/reports/PROP-20.10-SLOP01-to-PM01.md
pm_disposition: docs/agents/reports/PROP-20.10-PM01-accept.md
---

# PROP-20.11 — Wave A slop cleanup F1–F7 + F9 — **Pass**

## Verdict

PM dispositions from PROP-20.10 applied. F8 `ChatContextBuilder.BuildMemoryBlock` façades **kept**. No new features. Targeted suites green (**118/118**).

## F-ID results

| ID | Disposition | Result | What changed |
| --- | --- | --- | --- |
| **F1** | remove `SmsTools` | **Pass** | Deleted dead alias class from `SmsToolGate.cs` |
| **F2** | dedupe truncate → composer | **Pass** | `SmsOutboundComposer.TruncateForCarrier`; inbound/outbound call it; removed `TruncateForSms` / private `Truncate` twins |
| **F3** | dedupe strip → UntrustedObservation | **Pass** | `UntrustedObservation.StripWhereMarked<T>`; `StripFromPreamble` + `MemoryTape.StripUntrusted` both use it |
| **F4** | one write-as-her name | **Pass** | Kept `MayWriteAsHer` only; removed `ContainsRequiredMind`; runner + tests updated |
| **F5** | remove `SharedBuilder` | **Pass** | Hook + silent catch removed; test uses `LeisureMindBlock.Build(..., floors:)` |
| **F6** | Core markers; drop temp builder | **Pass** | Dropped `FloorsHeader`/`RestingHeader`/`Provisional*` re-exports + `BuildTemporaryMidDefaults`; tests use `VictoriaMindBlock.*` |
| **F7** | remove bare `[Resting]` | **Pass** | Gate requires `VictoriaMindBlock.RestingMarker` only; regression asserts bare `[Resting]` fails |
| **F8** | keep façade | **Pass** | Untouched — both `ChatContextBuilder.BuildMemoryBlock` overloads still delegate to `MemoryTape` |
| **F9** | XML fix | **Pass** | `IVictoriaMindBlockBuilder` summary no longer claims “related memories” |

## Evidence

**Command** (Debug; Host on `:7710` briefly stopped to unlock `SoulCore.Host` output copy, then restarted):

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Debug --filter "FullyQualifiedName~SmsOutboundComposer|FullyQualifiedName~SmsOutboundService|FullyQualifiedName~SmsInboundService|FullyQualifiedName~SmsSecurityGate|FullyQualifiedName~Prop17Leisure|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~UntrustedObservation|FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~MemoryRecall"
```

**Output:**

```text
Passed!  - Failed:     0, Passed:   118, Skipped:     0, Total:   118, Duration: 805 ms - SoulCore.Protocol.Tests.dll (net8.0)
```

Post-restart Host health (`http://127.0.0.1:7710/health`): `status=ok`, `inboundUsesToolLoop=true`.

## Files

| Path | Change |
| --- | --- |
| `SoulCore.Inference/Presence/SmsToolGate.cs` | F1 remove `SmsTools` |
| `SoulCore.Host/Companion/SmsOutboundComposer.cs` | F2 `TruncateForCarrier` |
| `SoulCore.Host/Companion/SmsInboundService.cs` | F2 callers; drop twin helpers |
| `SoulCore.Host/Companion/SmsOutboundService.cs` | F2 call composer (no inbound dep) |
| `SoulCore.Inference/Presence/UntrustedObservation.cs` | F3 `StripWhereMarked` |
| `SoulCore.Host/Ws/MemoryTape.cs` | F3 delegate strip |
| `SoulCore.Inference/Presence/LeisureMindBlock.cs` | F4–F7 cleanup |
| `SoulCore.Host/Loop/LeisureRunner.cs` | F4 `MayWriteAsHer` only |
| `SoulCore.Host/Companion/IVictoriaMindBlockBuilder.cs` | F9 XML |
| `SoulCore.Protocol.Tests/Prop17LeisureTests.cs` | F4–F7 test updates |

## Handoff

Ready for PM accept. Wave A hygiene P1 (F1–F3) + F4–F7/F9 closed; F8 kept by design.
