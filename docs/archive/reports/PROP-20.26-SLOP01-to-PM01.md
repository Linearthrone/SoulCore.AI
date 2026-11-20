---
type: report
prop_id: PROP-20.26
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.26
status: findings
created: 2026-11-17
related_qa: docs/agents/reports/PROP-20.25-QA01-to-PM01.md
related_dev: docs/agents/reports/PROP-20.24-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-20.26

## Scope

- Related QA: PROP-20.25 Pass (operator weights + Release smoke)
- Related DEV: PROP-20.24 BED bake Dominant 0.70 Mid / Ambitious+Uninhibited 0.80 High `operator`
- Files / packages scanned:
  - `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` (`DefaultRestingTraits`, weight/band/ledger consts, `AppendResting`)
  - `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` (`RestingTraitNames` mirror)
  - `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` (shared Build consumer)
  - `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs`
  - `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` (preamble Contains pins)
  - `SoulCore/SoulCore.Protocol.Tests/Prop17LeisureTests.cs` (name-gate only; no weight dup)
- Method: read ticket + QA/BED/decision; ripgrep for `DefaultRestingTraits` / weight consts / hard-coded `0.70`/`0.80` / Mid052 leftovers / parallel trait arrays; full read of change-surface sources

## Summary

- Findings count: **1**
- Highest severity: **P2**
- Verdict: **findings** (non-blocking comment drift only)

## Findings

### F1 — Stale “Mid defaults” XML / class comments after operator High bake

- Category: slop
- Severity: P2
- Evidence:
  - `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` class summary still says “resting Mid-default trait set” (lines ~7–11) while `DefaultRestingTraits` now mixes Mid provisional + High operator rows
  - `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` class summary still says “resting Mid defaults” (line ~6) after rename to `…WithOperatorWeights`
  - Adjacent (not in BED file list): `VictoriaMindBlockBuilder.cs` summary + catch comment still say “resting Mid defaults” (~8, ~34)
- Why it matters: Comments disagree with shipped resting set; next reader may assume all rows are still 0.52 Mid provisional.
- Recommended action: remove (refresh wording to “operator resting weights / mixed Mid+High”)
- Suggested owner if remove/dedupe: BED-01
- Ask-user question (if ask-user): —

## Clean areas

- **Single source of truth:** Product resting rows remain only in `VictoriaMindBlock.DefaultRestingTraits`; weights use named consts (`RestingDominantWeight`, `RestingHighOperatorWeight`, `RestingMidDefaultWeight`); no second hard-coded Dominant/Ambitious/Uninhibited product array.
- **Leisure mirror still derived:** `RestingTraitNames` = `DefaultRestingTraits.Select(t => t.Name).ToArray()` — no duplicate name list introduced by 20.24.
- **No alias sprawl:** `RestingLedgerReason` vs `RestingOperatorLedgerReason` and `RestingBand` vs `RestingBandHigh` are intentional splits matching `prop-20-resting-seed-decision.md`.
- **No dead Mid052 lock test:** Old `AreNineLockedNamesAtMid052` replaced; no leftover product assert that all nine are 0.52/same ledger.
- **Test magic numbers are lock pins,** not product duplication (intentionally hardcode 0.70/0.80 so const drift still fails).
- **No `any`, abnormal try/catch, or second Build path** on this delta; Host/leisure still call shared `VictoriaMindBlock.Build`.
- **AppendResting mixed-ledger header** matches the three-operator / six-provisional split; no parallel formatter.

## Notes for PM

- Safe to archive? **yes** with F1 deferred, or one-liner BED comment refresh if PM wants zero open SLOP items
- Prefer DEV cleanup before user-facing “done”? **no** — not blocking Release / PROP-20.1d; product weight bake is sound
- No remove/dedupe of product logic recommended from this pass
)
