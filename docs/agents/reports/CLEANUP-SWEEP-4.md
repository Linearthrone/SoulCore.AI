---
type: report
id: CLEANUP-SWEEP-4
from: CLEANUP-TEAM
branch: cursor/prop17-cleanup-sweeps-6372
created: 2026-10-12
status: complete
---

# CLEANUP Sweep 4 — FINAL verification

Branch: `cursor/prop17-cleanup-sweeps-6372`  
Prior: Sweeps 1–3 (SoulCore + House); Sweep 3 leftovers commit `981012c`.

## Scope

Final mandatory verification of PROP-17 surface only after Sweep 3 found/fixed items.
No invent-churn on documented leave-alones.

## findings_fixed (2)

| # | Location | Change |
|---|----------|--------|
| 1 | `Mt4ToolSupport.IsTruthyString` | Remove dead private helper (never called) |
| 2 | `MainWindow.RunServiceActionAsync` | Reuse `hostStackTag` for status writes (drop repeated tag checks) |

**findings_fixed count: 2**

Left alone on purpose: `TryValidateStopLoss` empty `// ok` parse arms — reshaping them either trips definite-assignment or expands the method without clarifying intent.

## Leave-alones (confirmed, not churn)

- Parallel C# `EmailMailboxChoice` vs Kotlin `EmailMailboxLabels` (no shared runtime)
- Dual `NotingRegistry` (divergent ExecuteAsync semantics)
- Email/Mt4 `Is*Allowed` overloads on `(IToolsAccessSettings)` vs `(ToolsOptions)`
- Honest Host-awaiting activity-band / Identity stub copy
- `RunServiceActionAsync` switch shape (needs dedicated Host Stop regression before reshape)
- TaskCreate/Update catch contracts; PlaywrightBrowserBridge mega-surface
- Schema/002+ unused embeddings (Memory ownership; 008–010 already un-embedded)

## FINDINGS_REMAINING

**FINDINGS_REMAINING=no**

## CLEANUP_COMPLETE

**yes** — no further actionable PROP-17 deslop remains after this pass. No Sweep 5.

## Residual risk

- Low: `TryValidateStopLoss` parse logic unchanged (number or invariant string → finite positive).
- Low: HostStack status include flag still keyed off the same host tags.
- Naming: LinearThrone / Kayleigh / Victoria preserved.

## Tests

```text
dotnet test SoulCore/SoulCore.Protocol.Tests \
  --filter "FullyQualifiedName~Prop17|FullyQualifiedName~TaskTools|FullyQualifiedName~WorkContinuation|FullyQualifiedName~GuestWorkspace|FullyQualifiedName~EmailTools|FullyQualifiedName~Mt4Tools"

Passed!  Failed: 0, Passed: 125, Skipped: 0, Total: 125
```

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj

Passed!  Failed: 0, Passed: 102, Skipped: 0, Total: 102
```

Logs: `/opt/cursor/artifacts/sweep4-prop17-tests.log`, `/opt/cursor/artifacts/sweep4-house-chatdesktop-tests.log`

Code SHA: `452023b`
