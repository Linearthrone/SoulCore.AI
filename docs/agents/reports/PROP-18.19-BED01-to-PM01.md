---
type: report
prop_id: PROP-18.19
from: BED-01
to: PM-01
priority: P2
status: Pass
created: 2026-11-19
depends_on: PROP-18.14
related_task: docs/agents/tasks/PROP-18.19-PM01-to-BED01.md
related_slop: docs/agents/reports/PROP-18.14-SLOP01-to-PM01.md
related_accept: docs/agents/reports/PROP-18.14-PM01-accept.md
---

# PROP-18.19 — BED-01 → PM-01

## Verdict

**Pass**

## Scope

SLOP PROP-18.14 hygiene F1 + F2 (PM accept disposition).

| Finding | Action | Result |
| --- | --- | --- |
| **F1** | `/health` use `SafeParkedBudgetSnapshot` (dedupe try/catch) | Done |
| **F2** | Keep `IsExemptKind`; wire production exempt / consume paths through it | Done |

## Changes

### F1 — health safe snapshot

`SoulCore.Host/Hosting/WebApplicationExtensions.cs` `/health`:

- Removed inline `try { GetSnapshot() } catch { null-DTO }`.
- Now mirrors `/settings/identity`: `IdleParkedBudgetDto(SafeParkedBudgetSnapshot(parkedBudget))`.

### F2 — `IsExemptKind` on product path

Kept `FileIdleParkedWorkBudget.IsExemptKind` as the single Q3 law catalog. Production surfaces now call it (no parallel hard-coded exempt lists):

| Surface | Kind(s) | Gate |
| --- | --- | --- |
| `LeisureRunner.RunOnePassAsync` | `leisure` | must be exempt |
| `LeisureRunner.WriteJournalNoteAsync` | `journal` | must be exempt |
| `LeisureRunner.WriteActivityResidueAsync` | `leisure` (non-research) | must be exempt |
| `SoulLoopHostedService.ExecuteAsync` | `soulloop` / `soul-loop` | must be exempt |
| `AssignedWorkRunner.TryProbeParkedAsync` | `parked-work` | must **not** be exempt |
| `AssignedWorkHostedService.EnsureProbeCandidateAsync` | `parked-work` | must **not** be exempt |

Catalog drift throws `InvalidOperationException` so future edits cannot silently diverge.

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests"

Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7
SoulCore.Host -> …\SoulCore.Host.dll  (build OK)
```

Test update: `LeisureJournalActivityLog_AreExemptKinds` now asserts `soul-loop` and that `research` is not exempt.

## Notes for PM

- Ready to archive with PROP-18.14 hygiene closed.
- No OPS deploy required for this hygiene-only change.
