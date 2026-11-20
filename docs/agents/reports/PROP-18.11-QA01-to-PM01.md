---
type: report
prop_id: PROP-18.11
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.8-BED01-to-PM01.md
task: docs/agents/tasks/PROP-18.11-PM01-to-QA01.md
---

# PROP-18.11 — QA idle parked B3 budget — Pass

## Result

**Pass.** Q3 B3 soft budget verified: ≤**3** unsupervised parked advances **or** **45 min** tool wall-clock per local calendar day (whichever first); serial one-at-a-time; leisure / journal / activity-log / soulloop exempt; live workspace Host exposes `idleParkedBudget` on `/health` and `/settings/identity`.

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| ≤3 advances / local day | **Pass** | Isolated harness: 3 advances then 4th blocked; `exhaustReason=advances_cap` |
| ≤45 min tool time / day | **Pass** | 44 + 1.5 min → exhausted; new advance blocked; `exhaustReason=tool_time_cap` |
| Serial (one active) | **Pass** | Same task re-enter OK; different id refused while open; after `EndAdvance` next id OK |
| Leisure exempt | **Pass** | `IsExemptKind` true for leisure/journal/activity-log/soulloop; `RecordToolTime` without active advance is no-op (0 advances / 0 tool min) |
| Day rollover + persist | **Pass** | Next local day resets; reload from JSON keeps counters |
| `idleParkedBudget` on `/health` | **Pass** | Workspace Host `http://127.0.0.1:7710/health` |
| `idleParkedBudget` on `/settings/identity` | **Pass** | Workspace Host `http://127.0.0.1:7710/settings/identity` |

## Live Host probe (workspace :7710)

```
7710_health.version=1.0.0
7710_health.has_idleParkedBudget=True
{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null}

7710_identity.has_idleParkedBudget=True
{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null}
```

Caps match law: `advancesCap=3`, `toolMinutesCap=45`.

### Note on :7700

Foreign stale Host on `:7700` (`C:\Users\kurtw\Soul_Core\...`, v0.1.52) does **not** expose `idleParkedBudget`. Known as `docs/agents/issues/ISSUE-20261118-001-stale-foreign-host-on-7700.md`. QA gate target is House Victoria Host **:7710**.

## Budget unit evidence (isolated harness)

Official filter rebuild blocked by unrelated CS1061 (see Issues). QA ran equivalent B3 checks against current `IdleParkedWorkBudget.cs` via `tmpcode/prop1811-qa` (not product code).

```
CONST MaxAdvancesPerDay=3
CONST MaxToolTimeMinutes=45
PASS: advances_cap_const_3
PASS: tool_cap_const_45
PASS: exempt_leisure
PASS: exempt_journal
PASS: exempt_activity_log
PASS: exempt_soulloop
PASS: not_exempt_parked
PASS: a1
PASS: a2
PASS: a3
PASS: cannot_start_after_3
PASS: block_4th
PASS: snap_advances_cap
PASS: serial_first
PASS: serial_reenter_same
PASS: serial_block_other
PASS: serial_after_end
PASS: tool_start1
PASS: tool_start2
PASS: tool_exhausted
PASS: tool_block_new
PASS: snap_tool_cap
PASS: leisure_noop
PASS: d1a1
PASS: d1a2
PASS: d1a3
PASS: d1_exhausted
PASS: d2_reset
PASS: d2_advance
PASS: d2_snap
PASS: persist_begin
PASS: persist_reload
ALL_PROP1811_BUDGET_CHECKS_PASSED
```

## Official suite attempt (blocked)

```
dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests"

ActivityResidueComposer.cs(150,19): error CS1061: 'IReadOnlyList<string>' does not contain a definition for 'Add' ...
Build FAILED.  1 Error(s)
```

Filed: `docs/agents/issues/ISSUE-20261119-001-ActivityResidueComposer-CS1061-blocks-build.md` (P1 — blocks Inference rebuild / Prop188 filter). Does **not** fail PROP-18.11 B3 acceptance (logic + live exposure verified independently).

## Integration wiring (spot-check)

| Location | Behavior |
| --- | --- |
| `AssignedWorkHostedService.EnsureProbeCandidateAsync` | Skips parked probe when `!_budget.CanStartAdvance()` |
| `AssignedWorkRunner.TryProbeParkedAsync` | `TryBeginAdvance` / refuse when exhausted or serial-busy; `_budgetSession` tool time |
| `LeisureSession` | No `IIdleParkedWorkBudget` / `TryBeginAdvance` / `RecordToolTime` references |
| DI | `IIdleParkedWorkBudget` → `FileIdleParkedWorkBudget` in Companion services |

## Issues

| ID | Severity | Notes |
| --- | --- | --- |
| ISSUE-20261119-001 | P1 | `ActivityResidueComposer` CS1061 blocks official Prop188 rebuild |
| ISSUE-20261118-001 | P2 | Stale foreign Host on :7700 (pre-existing; :7710 OK) |

## Handoff

PM-01 may accept PROP-18.11. Dispatch **SLOP-01** for post-QA slop audit. Recommend BED fix ISSUE-20261119-001 so `Prop188IdleParkedBudgetTests` can run via solution gate again.
