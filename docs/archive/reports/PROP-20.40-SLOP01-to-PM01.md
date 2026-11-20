---
type: report
prop_id: PROP-20.40
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.40
status: findings
created: 2026-11-18
title: SLOP Step 7 growth Host surface (PROP-20.37)
related_qa: docs/agents/reports/PROP-20.39-QA01-to-PM01.md
upstream: docs/agents/reports/PROP-20.37-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-20.40

**Result: findings** (P2 only; no P0/P1)

Read-only audit of PROP-20.37 growth / soak / band-cross Host surface after QA Pass (PROP-20.39).

## Scope

- Related QA: `docs/agents/reports/PROP-20.39-QA01-to-PM01.md` (Pass)
- Related DEV: `docs/agents/reports/PROP-20.37-BED01-to-PM01.md`
- Files / packages scanned:
  - `SoulCore.Core/Growth/` — `GrowthBands.cs`, `GrowthSoakClock.cs`, `GrowthDeltaEngine.cs`, `GrowthTraits.cs`
  - `SoulCore.Core/VictoriaMindBlock.cs` — `[Growth traits]` append path
  - `SoulCore.Memory/IGrowthStore.cs`, `Repositories/SqliteGrowthRepository.cs`
  - `SoulCore.Memory/Migrations/014_growth_bond.sql`
  - `SoulCore.Host/Growth/` — `IGrowthService.cs`, `GrowthService.cs`
  - `SoulCore.Host/Hosting/WebApplicationExtensions.cs` — Identity + confirm/dismiss
  - `SoulCore.Host/Ws/ChatContextBuilder.cs`, `Companion/VictoriaMindBlockBuilder.cs` — prompt gate wiring
  - `SoulCore.Protocol.Tests/Prop2037GrowthBondTests.cs`
- Method: read BED file list + ripgrep (`GrowthService|IGrowthStore|ComputeBondCounters|GetPromptGrowthTraits|mayChangePrompt|operator-provisional`) + full-file reads (no product edits)

## Summary

- Findings count: **2**
- Highest severity: **P2**

## Findings

### F1 — Soak/gate evaluation copy-pasted four times in `GrowthService`

- Category: duplicate
- Severity: P2
- Evidence:
  - `SoulCore.Host/Growth/GrowthService.cs` — same block in:
    - `GetIdentitySnapshotAsync` (~L27–38; gate window variant + second full-window counters)
    - `GetPromptGrowthTraitsAsync` (~L70–75)
    - `ApplyProposedDeltaAsync` (~L90–95)
    - `EvaluateCounterNudgesAsync` (~L192–197)
  - Pattern: `soakWindowEnd = started + DisplayOnlyDuration` → `ComputeBondCountersAsync(started, gateEnd)` → `GrowthSoakClock.Evaluate(...)`
  - Minor style twin: `soakWindowEnd < now ? soakWindowEnd : now` vs `now < soakWindowEnd ? now : soakWindowEnd` (same math)
- Why it matters: future soak/gate rule changes must be edited in four places; drift risk (Identity vs prompt vs apply vs daily nudge).
- Recommended action: **dedupe**
- Suggested owner: **BED-01**
- Suggested fix (minimal): private helper e.g. `ResolveSoakAsync(started, now, ct)` returning `(SoakSnapshot soak, BondCounterSnapshot gateCounters)`; keep the Identity-only second `ComputeBondCountersAsync(started, now)` for display counters outside the helper.

### F2 — Growth seed rows duplicated in SQL migration and C# defaults

- Category: duplicate
- Severity: P2
- Evidence:
  - `SoulCore.Memory/Migrations/014_growth_bond.sql` INSERT (~L63–73) — three traits @ 0.52 Mid + identical `prompt_line` / `ledger_reason`
  - `SoulCore.Core/Growth/GrowthTraits.cs` `DefaultSeedTraits` (~L25–49) — same three lines / reason `operator-provisional-until-seed`
  - Fallback path: `SqliteGrowthRepository.ListTraitWeightsAsync` returns `DefaultSeedTraits` when table empty (~L107–108)
- Why it matters: prompt copy or seed weight edits can diverge between migration and runtime fallback.
- Recommended action: **ask-user**
- Ask-user question: Keep dual seed (SQL for migrate + C# for empty-table fallback) as intentional, or make C# `DefaultSeedTraits` the single source (migration stays snapshot / tests assert parity)?

## Clean areas

- `GrowthBands` / `GrowthSoakClock` / `GrowthDeltaEngine` — focused pure rules; no `any`-style casts; comments match law, not restating noise.
- Confirm/dismiss HTTP twins in `WebApplicationExtensions` are intentionally parallel and lean (no extra wrappers).
- Desk (`ChatContextBuilder`) vs SMS (`VictoriaMindBlockBuilder`) both call `GetPromptGrowthTraitsAsync` — intentional dual surfaces, not same-purpose aliases.
- `VictoriaMindBlock.AppendGrowthTraits` mirrors existing resting/compass section style; omit-when-null gate is correct.
- Identity field mapping (`bondCounters` / `soak` / `growthTraits` / `pendingBandCross` / `growthLedger`) is straightforward DTO projection; empty fallback branch matches existing charter try/catch resilience pattern in the same handler.
- No same-purpose alias pair like `getGrowth` / `fetchGrowth` / `loadGrowth` beyond the clear store vs service layering (`IGrowthStore` / `IGrowthService`).

## Notes for PM

- Safe to archive? **yes, with optional defer** — findings are P2 hygiene; QA Pass stands; FED-20.38 can consume Identity fields without waiting on cleanup.
- If cleanup: ticket **BED-01** for F1 (dedupe soak helper); resolve F2 with user or accept dual-seed risk and archive.
- Not flagged (intentional / out of slop bar):
  - Side-effect nudge evaluate inside Identity GET (product choice; try/catch logs Debug).
  - `GrowthBands.IsInsideBand` thin wrapper (used by tests + public API symmetry).
  - `RestingLedgerReason` vs `GrowthTraits.LedgerReasonSeed` sharing the same string — domain-separated constants; leave unless F2 seed consolidation lands.
