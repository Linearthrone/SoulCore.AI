---
type: report
prop_id: PROP-20.41
from: BED-01
to: PM-01
task_id: PROP-20.41
status: Pass
created: 2026-11-18
title: SLOP cleanup — ResolveSoakAsync + DefaultSeedTraits SoT parity
depends_on: PROP-20.40
upstream_task: docs/agents/tasks/PROP-20.41-PM01-to-BED01.md
upstream_slop: docs/agents/reports/PROP-20.40-SLOP01-to-PM01.md
upstream_accept: docs/agents/reports/PROP-20.40-PM01-accept.md
---

# PROP-20.41 — BED-01 → PM-01

**Result: Pass**

F1 soak/gate dedupe and F2 C# seed SoT + parity gate landed. Growth bond suite green (15/15).

## F1 — `ResolveSoakAsync`

- Added private `GrowthService.ResolveSoakAsync(started, now, ct)` returning `(SoakSnapshot, BondCounterSnapshot gateCounters)`.
- Wired into:
  - `GetIdentitySnapshotAsync` (still does separate `ComputeBondCountersAsync(started, now)` for display-to-now)
  - `GetPromptGrowthTraitsAsync`
  - `ApplyProposedDeltaAsync`
  - `EvaluateCounterNudgesAsync`
- File: `SoulCore/SoulCore.Host/Growth/GrowthService.cs`

## F2 — `DefaultSeedTraits` SoT + parity

- PM accept: C# `GrowthTraits.DefaultSeedTraits` is SoT; migration 014 INSERT remains bootstrap snapshot.
- Documented SoT on `DefaultSeedTraits` and on the INSERT in `Migrations/014_growth_bond.sql`.
- Added parity test `Migration014_SeedInsert_MatchesDefaultSeedTraits` in `Prop2037GrowthBondTests` — asserts INSERT header/weight/band + each `prompt_line` / `ledger_reason` from C# SoT appear in embedded `014_growth_bond.sql` (EOL-normalized).
- Empty-table fallback in `SqliteGrowthRepository.ListTraitWeightsAsync` already returns `DefaultSeedTraits` (unchanged).

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~Prop2037GrowthBondTests"
Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15
```

## Files touched

| Path | Change |
| --- | --- |
| `SoulCore.Host/Growth/GrowthService.cs` | F1 helper + call sites |
| `SoulCore.Core/Growth/GrowthTraits.cs` | SoT doc comment |
| `SoulCore.Memory/Migrations/014_growth_bond.sql` | Bootstrap/SoT comment |
| `SoulCore.Protocol.Tests/Prop2037GrowthBondTests.cs` | F2 parity test |

## Handoff

Ready for PM accept / archive. No OPS deploy required (Host logic + test only).
