# CLEANUP Sweep 2 — SoulCore PROP-17

Branch: `cursor/prop17-cleanup-sweeps-6372`  
Base: Sweep 1 `67418cc` / report `490b85a`  
Scope: SoulCore PROP-17 deferred items only. No `House/**`. No PlaywrightBrowserBridge / TaskCreateTool catch contracts.

## Findings fixed (file:symbol)

| # | Location | Change |
|---|----------|--------|
| 1 | `LeisureEligibility.TryParseParkUntil` | Shared invariant-UTC parse for park-until ISO `...Z` |
| 2 | `LeisureEligibility.IsParkProbeDue` | Use `TryParseParkUntil` (same culture/styles as runner) |
| 3 | `AssignedWorkRunner.TryProbeParkedAsync` | Delegate parse to `TryParseParkUntil`; drop local CultureInfo import |
| 4 | `AssignedWorkHostedService.EnsureProbeCandidateAsync` | Use `IsParkProbeDue` instead of bare `TryParse` + manual compare |
| 5 | `SoulCore.Memory.csproj` | Stop embedding unused `Schema/008–010` (never `ReadEmbedded`) |
| 6 | `Schema/008|009|010_*.sql` headers | Document companions-only; runtime path is `Migrations/` |
| 7 | `Prop17ParkAndSopTests.ParkUntil_Parse_IsInvariantUtc` | Lock parse + probe-due semantics |

**Cleanup count: 7**

## Deferred / leave-alone (with why)

### NotingRegistry — leave alone
`LeisureRunner.NotingRegistry` wraps snapshot/screenshot/read content via `UntrustedObservation` and tracks `UntrustedChunks`.  
`AssignedWorkRunner.NotingRegistry` records `Names` + `(Name, Success, Content)` for failure-key / progress logic and returns results unchanged.  
Shared surface is only “decorate `IToolRegistry` + fire `onTool`”; `ExecuteAsync` semantics diverge. A shared base would either force optional hooks into both paths or hide the difference — not worth the abstraction.

### Email/Mt4 gate overloads — leave alone
After Sweep 1 `ConsentArgs`, remaining parallels are `IsReadAllowed` / `IsTradeAllowed` / `IsSendAllowed` / `IsDeleteAllowed` overloads on `(IToolsAccessSettings)` vs `(ToolsOptions)`. Each is a one-line property check on domain-specific flags (`AllowEmail*` vs `AllowMt4*`). Cross-family consolidation would need awkward generics or lambdas with no call-site clarity gain. Risk &gt; payoff.

### TaskCreateTool / TaskUpdateStatusTool catch contracts — out of scope
### PlaywrightBrowserBridge mega-refactor — out of scope

## Schema/ vs Migrations/ 008–010 (packaging verdict)

| Side | Role |
|------|------|
| `Migrations/008–010` | **Runtime apply path** — `SqliteMemorySession.ApplyMigrations` → `ReadEmbedded("SoulCore.Memory.Migrations.…")` |
| `Schema/008–010` | **Documentation companions** — DDL/shape notes; Schema/010 has no executable SQL |

Only `Schema/001` is applied at runtime (fresh install). Schema/002+ companions have historically been embedded even when unused; PROP-17’s 008–010 embeddings were never referenced by `ApplyIfMissing` and Schema/010 was an empty comment stub. Sweep 2 **removed the dead embeddings** while keeping the Schema files on disk for humans. Broader unused-Schema embedding cleanup (002/005/006/007) left for Memory ownership.

## Residual risk

- Low: Aligning HostedService/LeisureEligibility park parse to invariant+AssumeUniversal matches how `ParkAsync` writes timestamps; bare `TryParse` already accepted those strings in practice. Covered by new unit test + park round-trip.
- Low: Dropping Schema/008–010 embeddings cannot affect runtime (never loaded). Migrations embeddings unchanged.
- Naming: LinearThrone / Kayleigh preserved.

## Tests

```text
dotnet test SoulCore/SoulCore.Protocol.Tests \
  --filter "FullyQualifiedName~Prop17|FullyQualifiedName~TaskTools|FullyQualifiedName~WorkContinuation|FullyQualifiedName~GuestWorkspace|FullyQualifiedName~EmailTools|FullyQualifiedName~Mt4Tools"

Passed!  Failed: 0, Passed: 125, Skipped: 0, Total: 125
```

Log: `/opt/cursor/artifacts/sweep2-prop17-tests.log`  
(+1 vs Sweep 1 from `ParkUntil_Parse_IsInvariantUtc`.)

## Sweep 3 recommendation

**SHOULD_SWEEP_3=no** — deferred items are intentional leave-alones or out-of-scope refactors; no concrete low-risk same-purpose remnant remains in this SoulCore PROP-17 surface.
