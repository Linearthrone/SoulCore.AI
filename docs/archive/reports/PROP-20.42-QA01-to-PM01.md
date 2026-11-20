---
type: report
prop_id: PROP-20.42
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
priority: P1
status: Pass
created: 2026-11-18
title: QA PROP-20.41 growth hygiene
depends_on: PROP-20.41
upstream: docs/agents/reports/PROP-20.41-BED01-to-PM01.md
---

# PROP-20.42 — QA-01 → PM-01

**Result: Pass**

PROP-20.41 growth hygiene verified with real `dotnet test`. `ResolveSoakAsync` soak/gate path still correct through GrowthService call sites; F2 seed parity gate (`Migration014_SeedInsert_MatchesDefaultSeedTraits`) green; full growth/soak/band-cross suite **15/15**.

## Verdict matrix

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | `ResolveSoakAsync` wired; soak/gate path correct | **Pass** | Service tests exercise helper via `GetIdentitySnapshotAsync` / `GetPromptGrowthTraitsAsync` / `ApplyProposedDeltaAsync`: display-only reject, post-soak open, contact-gate closed |
| 2 | Seed parity: migration 014 INSERT ↔ `DefaultSeedTraits` | **Pass** | `Migration014_SeedInsert_MatchesDefaultSeedTraits` Passed (&lt;1 ms) |
| 3 | Soak display-only before 14d | **Pass** | `SoakClock_DisplayOnly_*` + `Service_DisplayOnly_*` → `phase=display_only mayChange=False decision=Rejected/pre_soak_display_only` |
| 4 | Contact gate closed when &lt;3 contact days | **Pass** | `SoakClock_GateClosed_*` + `Service_ContactGate_*` → `contactDays=1 phase=gate_closed` |
| 5 | Inside-band auto-apply + band-cross pending/confirm | **Pass** | `DeltaEngine_InsideBand_*` / `BandCross_*` + `Service_PostSoak_*` → `inside=AutoApply cross=Pending confirmedWeight=0.85 promptGrowth=3` |
| 6 | Bond counters + migration seed | **Pass** | `Store_Migration014_SeedsTraits_AndBondCounters` → `days=3 minutes=5 tasks=0` |
| 7 | Full Prop2037GrowthBondTests suite | **Pass** | **15/15** (was 14; +1 parity test from PROP-20.41) |

## Commands run

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~Prop2037GrowthBondTests" --verbosity normal
```

## Test evidence (real output)

### Prop2037GrowthBondTests — 15/15 Pass

```text
[PROP-20.37] gate-closed contactDays=1 phase=gate_closed
[PROP-20.37] open inside=AutoApply cross=Pending confirmedWeight=0.85 promptGrowth=3
[PROP-20.37] bondCounters days=3 minutes=5 tasks=0
[PROP-20.37] display-only phase=display_only mayChange=False decision=Rejected/pre_soak_display_only

  Passed ...DeltaEngine_PreSoak_Rejects
  Passed ...DeltaEngine_InsideBand_AutoApplies
  Passed ...DeltaEngine_Rejects_NonNudgeableTrait
  Passed ...Service_ContactGate_BlocksOpen_EvenAfterFourteenDays
  Passed ...DeltaEngine_BandCross_StaysPending
  Passed ...Service_PostSoak_InsideBandApplies_BandCrossPending_Confirm
  Passed ...DeltaEngine_FirstWeek_ClipsToSmallNudge
  Passed ...Store_Migration014_SeedsTraits_AndBondCounters
  Passed ...SoakClock_Opens_AfterFourteenDays_WithThreeContactDays
  Passed ...SoakClock_DisplayOnly_BeforeFourteenDays
  Passed ...GrowthBands_ResolvePreferredRanges
  Passed ...Migration014_SeedInsert_MatchesDefaultSeedTraits
  Passed ...Service_DisplayOnly_RejectsDelta_NoPromptTraits
  Passed ...SoakClock_GateClosed_WhenFewerThanThreeContactDays
  Passed ...MindBlock_OmitsGrowth_WhenNull_IncludesWhenProvided

Test Run Successful.
Total tests: 15
     Passed: 15
 Total time: 1.8909 Seconds

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.93
```

### ResolveSoakAsync path check (source + behavioral)

| Call site | Exercised by | Observed |
| --- | --- | --- |
| `GetIdentitySnapshotAsync` | `Service_DisplayOnly_*`, `Service_ContactGate_*` | Correct soak phase (`display_only` / `gate_closed`) |
| `GetPromptGrowthTraitsAsync` | `Service_DisplayOnly_*` (null), `Service_PostSoak_*` (3 traits) | Prompt traits gated by `soak.MayChangePrompt` |
| `ApplyProposedDeltaAsync` | Display-only reject / post-soak apply / gate reject | Delta decisions match soak state |
| `EvaluateCounterNudgesAsync` | Covered by same helper; no separate failing path | Helper present; suite green |

F2 parity: embedded `014_growth_bond.sql` INSERT headers/weights/bands/`prompt_line`/`ledger_reason` match C# `GrowthTraits.DefaultSeedTraits` (EOL-normalized).

## Issues

None. No new ISSUE files.

## Ready for

- **SLOP-01** post-QA slop audit (code-change Pass from PROP-20.41)
- PM-01 accept / archive PROP-20.41 + PROP-20.42

## QA red lines

No product code modified. No OPS deploy performed. Evidence only.
