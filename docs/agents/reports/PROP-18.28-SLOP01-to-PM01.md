---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.28
prop_id: PROP-18.28
prop_root: PROP-18-chronology-and-felt-persistence
status: clean
created: 2026-11-19
related_task: docs/agents/tasks/PROP-18.28-PM01-to-SLOP01.md
related_slop: docs/agents/reports/PROP-18.14-SLOP01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.19-BED01-to-PM01.md
depends_on: PROP-18.23
---

# SLOP-01 Report — PROP-18.28 (F1/F2 re-check)

## Scope

- Related prior SLOP: `docs/agents/reports/PROP-18.14-SLOP01-to-PM01.md` (findings F1/F2)
- Related cleanup: `docs/agents/reports/PROP-18.19-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (`/health`, `/settings/identity`, `SafeParkedBudgetSnapshot`, `IdleParkedBudgetDto`)
  - `SoulCore/SoulCore.Inference/Presence/IdleParkedWorkBudget.cs` (`IsExemptKind`)
  - `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs` (leisure / journal / residue gates)
  - `SoulCore/SoulCore.Host/Loop/SoulLoopHostedService.cs` (soulloop / soul-loop)
  - `SoulCore/SoulCore.Host/Loop/AssignedWorkRunner.cs` (`TryProbeParkedAsync` parked-work)
  - `SoulCore/SoulCore.Host/Loop/AssignedWorkHostedService.cs` (`EnsureProbeCandidateAsync` parked-work)
  - Tests: `Prop188IdleParkedBudgetTests.cs` (exempt catalog assertions)
- Method: read primary sources + ripgrep for `SafeParkedBudgetSnapshot` / `IsExemptKind` / `idleParkedBudget` / `GetSnapshot`
- **Read-only.** No code modified.

## Summary

- Findings count: **0** (new)
- Highest severity: **n/a**
- Verdict: **clean**
- PROP-18.14 **F1** and **F2**: **closed** (confirmed in product code)

## Closure check — PROP-18.14 findings

### F1 — `/health` safe snapshot dedupe — **CLOSED**

- Prior: health used inline `try { GetSnapshot() } catch { null-DTO }` while identity used `SafeParkedBudgetSnapshot`.
- Now: `/health` ~151–152:

  `var idleParkedBudget = IdleParkedBudgetDto(SafeParkedBudgetSnapshot(parkedBudget));`

  same shape as `/settings/identity` ~985. Single helper ~1377–1388 remains the only failure→null path. No second defensive shape on the B3 DTO surface.

### F2 — `IsExemptKind` unused on product path — **CLOSED**

- Prior: only tests called `IsExemptKind`; Host never gated through it.
- Now production call sites (catalog-drift throws on mismatch):

  | Surface | Kind(s) | Gate |
  | --- | --- | --- |
  | `LeisureRunner.RunOnePassAsync` | `leisure` | must be exempt |
  | `LeisureRunner.WriteJournalNoteAsync` | `journal` | must be exempt |
  | `LeisureRunner.WriteActivityResidueAsync` | `leisure` (non-research) | must be exempt |
  | `SoulLoopHostedService.ExecuteAsync` | `soulloop` / `soul-loop` | must be exempt |
  | `AssignedWorkRunner.TryProbeParkedAsync` | `parked-work` | must **not** be exempt |
  | `AssignedWorkHostedService.EnsureProbeCandidateAsync` | `parked-work` | must **not** be exempt |

- Tests still assert the Q3 catalog (`leisure`/`journal`/`activity-log|activity_log`/`soulloop|soul-loop` exempt; `parked-work`/`research` not). Helper is kept as intentional law catalog — matches PROP-18.19 disposition.

## Clean areas

- Single `SafeParkedBudgetSnapshot` for both health and identity B3 exposure.
- Single `IsExemptKind` catalog; no parallel hard-coded exempt lists on Host loop surfaces.
- No new duplicate / alias-sprawl on the idle parked budget surface in this delta.

## Notes for PM

- Safe to archive? **yes** — PROP-18.14 hygiene F1/F2 closed via PROP-18.19; this re-check is **clean**.
- No further DEV cleanup ticket from SLOP for this surface.
- Return for PM pipeline: **clean**
