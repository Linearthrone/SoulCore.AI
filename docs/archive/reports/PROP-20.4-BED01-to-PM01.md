---
type: report
prop_id: PROP-20.4
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
---

# PROP-20.4 — Mind block on desk chat (+ shared builder) — Pass

## Done

1. **Shared builder** `SoulCore.Core.VictoriaMindBlock` — Constitution floors (title + truncated floor line) + seven resting Mid defaults at **0.52** with ledger `operator-provisional-until-seed`. Callable without paste into SMS/leisure tickets.
2. **Injectable soft-dep** `IVictoriaMindBlockBuilder` / `VictoriaMindBlockBuilder` registered in Companion DI for 20.5/20.6/20.7.
3. **Desk chat wire** — `ChatContextBuilder` loads all charter rows as floors, emits Kayleigh address + mind block before `[Memory]`, keeps emotion outside the 16k budget. Coordinated with 20.2: `BuildMemoryBlock` remains delegated to `MemoryTape` (no ordering rewrite).
4. **ICharter** — `ListAnchorDetailsAsync` on the interface so floors get titles (not body-only).
5. Did **not** touch LeisureRunner/LeisureTools, SMS product logic beyond compile/DI soft-dep, or journal schema. No Ambitious/Proud; no context-trait Host selection.

## Resting set (shipped)

| Name | Weight | Band | Ledger |
| --- | --- | --- | --- |
| Dominant, Possessive, Affectionate, Driven, Devoted, Intense, Uninhibited | 0.52 | Mid | operator-provisional-until-seed |

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests"

Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 100 ms
```

Coverage includes: seven Mid lines always present; floors as `Title: line`; budget holds with memory + mind; charter-fail still emits resting; live compose injects Honesty/No-harm floors + all seven traits + `[Memory]`.

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | **new** shared floors + resting builder |
| `SoulCore/SoulCore.Core/Abstractions/ICharter.cs` | `ListAnchorDetailsAsync` |
| `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` | identity/mind preamble (memory via MemoryTape) |
| `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` | **new** DI adapter |
| `SoulCore/SoulCore.Host/Companion/IVictoriaMindBlockBuilder.cs` | soft-dep contract (parallel 20.7) |
| `SoulCore/SoulCore.Host/Hosting/.../CompanionServiceCollectionExtensions.cs` | register builder |
| `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` | **new** |
| `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` | floor/resting/budget cases |

## Acceptance

- [x] Desk chat preamble contains Constitution floors + seven resting lines
- [x] Shared builder callable without copying paste into SMS/leisure tickets
- [x] 16k budget still enforced; emotion outside
- [x] Tests + this report

## Handoff

- **20.6 / 20.7**: call `IVictoriaMindBlockBuilder.TryBuildAsync` or `VictoriaMindBlock.Build` — do not re-paste resting lines.
- **Step 5 (held)**: replace Mid 0.52 provisional weights when operator seeds close Open.
- **Step 4 (held)**: book-split will narrow which charter rows are Constitution floors; until then all live titles ship as floors.
