---
type: report
prop_id: PROP-18.23
from: QA-01
to: PM-01
priority: P1
status: Pass
created: 2026-11-19
depends_on: PROP-18.19
related_task: docs/agents/tasks/PROP-18.23-PM01-to-QA01.md
upstream: docs/agents/reports/PROP-18.19-BED01-to-PM01.md
---

# PROP-18.23 — QA-01 → PM-01

## Verdict

**Pass**

Ready for SLOP-01.

## Scope

Verify PROP-18.19 idle budget hygiene:

| Item | Criterion | Result |
| --- | --- | --- |
| **F1** | `/health` uses `SafeParkedBudgetSnapshot` (same helper as `/settings/identity`) | **Pass** |
| **F2** | `IsExemptKind` on leisure / journal / soul-loop (exempt) vs parked-work probes (not) | **Pass** |

## Acceptance matrix

| Check | Result | Evidence |
| --- | --- | --- |
| `/health` calls `IdleParkedBudgetDto(SafeParkedBudgetSnapshot(...))` | **Pass** | `WebApplicationExtensions.cs` ~151–152; no inline try/catch GetSnapshot |
| `/settings/identity` same helper | **Pass** | ~985 |
| Live `idleParkedBudget` on `/health` | **Pass** | Workspace Host `:7710` (below) |
| Live `idleParkedBudget` on `/settings/identity` | **Pass** | Workspace Host `:7710` (below) |
| Exempt: leisure / journal / soul-loop | **Pass** | Prop188 `LeisureJournalActivityLog_AreExemptKinds` + source gates |
| Not exempt: parked-work probes | **Pass** | Prop188 asserts false; `AssignedWorkRunner` / `AssignedWorkHostedService` drift throws if exempt |
| Prop188 suite | **Pass** | 7/7 |

## Source wiring (read-only)

**F1** — `/health` (~151–152):

```csharp
// PROP-18.19 / F1: same safe snapshot helper as /settings/identity (no duplicate try/catch).
var idleParkedBudget = IdleParkedBudgetDto(SafeParkedBudgetSnapshot(parkedBudget));
```

**F2** — production surfaces call `FileIdleParkedWorkBudget.IsExemptKind`:

| Surface | Kind | Expect |
| --- | --- | --- |
| `LeisureRunner.RunOnePassAsync` | `leisure` | must be exempt |
| `LeisureRunner.WriteJournalNoteAsync` | `journal` | must be exempt |
| `LeisureRunner.WriteActivityResidueAsync` | `leisure` (non-research) | must be exempt |
| `SoulLoopHostedService.ExecuteAsync` | `soulloop` / `soul-loop` | must be exempt |
| `AssignedWorkRunner.TryProbeParkedAsync` | `parked-work` | must **not** be exempt |
| `AssignedWorkHostedService.EnsureProbeCandidateAsync` | `parked-work` | must **not** be exempt |

## Prop188 test output (actual)

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests" --no-build -v n

  Passed Prop188IdleParkedBudgetTests.Persist_ReloadsCountersForSameLocalDay [33 ms]
  Passed Prop188IdleParkedBudgetTests.LeisureExemption_RecordingWithoutActiveAdvanceDoesNotConsume [2 ms]
  Passed Prop188IdleParkedBudgetTests.AdvancesCap_AllowsThreeThenBlocksFourth [2 ms]
  Passed Prop188IdleParkedBudgetTests.Serial_OneActiveParkedAdvanceAtATime [6 ms]
  Passed Prop188IdleParkedBudgetTests.ToolTimeCap_BlocksNewAdvancesAt45Minutes [2 ms]
  Passed Prop188IdleParkedBudgetTests.LeisureJournalActivityLog_AreExemptKinds [< 1 ms]
  Passed Prop188IdleParkedBudgetTests.LocalDayRollover_ResetsCounters [3 ms]
Test Run Successful.
Total tests: 7
     Passed: 7
```

`LeisureJournalActivityLog_AreExemptKinds` asserts: leisure / journal / activity-log / activity_log / soulloop / soul-loop **true**; parked-work / assigned-work / research / null **false**.

## Live health / identity probe (workspace :7710)

```text
STATUS=200
version=1.0.0 port=7710
has_idleParkedBudget=True
{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null}

RAW: "idleParkedBudget":{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null}

identity.has_idleParkedBudget=True
{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null}
```

Caps match B3 law: `advancesCap=3`, `toolMinutesCap=45`.

### Note on :7700

Foreign/stale Host on `http://127.0.0.1:7700/health` (v0.1.52) still omits `idleParkedBudget`. Known: `docs/agents/issues/ISSUE-20261118-001-stale-foreign-host-on-7700.md`. QA gate target remains House Victoria Host **:7710**.

## Issues

None new for PROP-18.23.

## Notes for PM

- Hygiene F1+F2 accepted at QA. Dispatch **SLOP-01** next.
- No OPS deploy required for this hygiene-only change (matches BED-18.19).
