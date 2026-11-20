---
type: report
prop_id: PROP-20.43
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.43
status: clean
created: 2026-11-18
title: SLOP delta — verify PROP-20.41 closed F1/F2
related_qa: docs/agents/reports/PROP-20.42-QA01-to-PM01.md
related_slop: docs/archive/reports/PROP-20.40-SLOP01-to-PM01.md
related_cleanup: docs/agents/reports/PROP-20.41-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-20.43

**Result: clean**

Read-only re-check: PROP-20.40 F1/F2 are closed by PROP-20.41. No new actionable slop on the cleanup surface.

## Scope

- Related QA: `docs/agents/reports/PROP-20.42-QA01-to-PM01.md` (Pass, 15/15)
- Prior SLOP: `docs/archive/reports/PROP-20.40-SLOP01-to-PM01.md` (findings F1/F2 P2)
- Cleanup: `docs/agents/reports/PROP-20.41-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore.Host/Growth/GrowthService.cs` — `ResolveSoakAsync` + call sites
  - `SoulCore.Core/Growth/GrowthTraits.cs` — `DefaultSeedTraits` SoT doc
  - `SoulCore.Memory/Migrations/014_growth_bond.sql` — bootstrap / SoT comment + INSERT
  - `SoulCore.Protocol.Tests/Prop2037GrowthBondTests.cs` — `Migration014_SeedInsert_MatchesDefaultSeedTraits`
  - `SoulCore.Memory/Repositories/SqliteGrowthRepository.cs` — empty-table fallback (unchanged, still SoT)
- Method: read PROP-20.40 findings + BED-01 file list + ripgrep (`ResolveSoakAsync|soakWindowEnd|GrowthSoakClock\.Evaluate|DefaultSeedTraits|Migration014_SeedInsert`) + targeted reads (no product edits)

## Summary

- Findings count: **0**
- Highest severity: **n/a**
- F1 closed: **yes**
- F2 closed: **yes**

## Closure verification

### F1 — `ResolveSoakAsync` (was: soak/gate copy-pasted ×4)

| Check | Evidence |
| --- | --- |
| Helper present | `GrowthService.ResolveSoakAsync` (~L238–250): window-cap → `ComputeBondCountersAsync(started, gateEnd)` → `GrowthSoakClock.Evaluate` |
| Call sites wired | `GetIdentitySnapshotAsync` L32; `GetPromptGrowthTraitsAsync` L65; `ApplyProposedDeltaAsync` L80; `EvaluateCounterNudgesAsync` L176 |
| No leftover duplicate soak block | `soakWindowEnd` / `GrowthSoakClock.Evaluate` appear **only** inside the helper |
| Identity display counters kept outside | L28–31 still `ComputeBondCountersAsync(started, now)` as intended by PROP-20.40 suggested fix |

**Verdict: closed**

### F2 — seed SoT + parity (was: SQL INSERT ↔ C# dual seed)

| Check | Evidence |
| --- | --- |
| C# is SoT | `GrowthTraits.DefaultSeedTraits` doc (~L25–29): SoT; migration 014 = bootstrap snapshot |
| Migration annotated | `014_growth_bond.sql` L63–64: bootstrap-only + parity gate name |
| Parity gate exists | `Migration014_SeedInsert_MatchesDefaultSeedTraits` asserts INSERT header/weight/band + `prompt_line` / `ledger_reason` vs SoT |
| Empty-table fallback | `SqliteGrowthRepository.ListTraitWeightsAsync` still returns `DefaultSeedTraits` (aligned with SoT) |
| QA confirmation | PROP-20.42: parity test Passed; suite **15/15** |

**Verdict: closed** (PM accept path: C# SoT + snapshot + parity — matches PROP-20.40 ask-user resolution)

## Findings

None.

## Clean areas

- Shared soak/gate path is single-source; Identity display-to-now counters correctly remain outside the helper.
- Seed dual representation is documented and gated; no unresolved alias-sprawl on growth seed.

## Notes for PM

- Safe to archive? **yes**
- PROP-20.40 F1/F2 closed; PROP-20.41 + PROP-20.42 + this delta form a complete cleanup chain.
- No further BED ticket needed for these items.
