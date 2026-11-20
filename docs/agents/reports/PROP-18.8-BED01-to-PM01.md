---
type: report
prop_id: PROP-18.8
prop_root: PROP-18-chronology-and-felt-persistence
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
---

# PROP-18.8 — Idle parked-work soft budget B3 — Pass

## Result

**Pass.** Host idle parked-work path enforces Q3 B3 soft budget: ≤**3** unsupervised parked advances **or** **45 min** cumulative tool wall-clock per **local calendar day** (whichever first); serial one-at-a-time; leisure / journal / activity-log / SoulLoop residue do not consume budget. Counters persist and are exposed on `/health` + `/settings/identity` as `idleParkedBudget`.

## Law implemented (Q3 B3)

| Rule | Enforcement |
| --- | --- |
| ≤3 parked advances / local day | `FileIdleParkedWorkBudget.TryBeginAdvance` increments; 4th refused |
| ≤45 min tool time on parked work | `RecordToolTime` on budget-session tool-loop; mid-session repark when tool cap hit |
| Serial (one at a time) | Active task slot + existing `RunningId`; different id refused while open |
| Leisure / journal / activity-log free | Exempt kinds helper; leisure never calls budget API; `RecordToolTime` no-op without active advance |
| Exhausted → no more parked advances; residue OK | `AssignedWorkHostedService.EnsureProbeCandidateAsync` skips when `!CanStartAdvance`; SoulLoop / leisure untouched |

## Changes

| Area | Detail |
| --- | --- |
| `SoulCore.Inference/Presence/IdleParkedWorkBudget.cs` | `IIdleParkedWorkBudget` + `FileIdleParkedWorkBudget` (JSON under `%LocalAppData%/SoulCore/idle-parked-work-budget.json`) |
| `AssignedWorkHostedService` | Skip parked probe pickup when budget cannot start |
| `AssignedWorkRunner` | `TryBeginAdvance` on parked→in_progress probe; tool wall-clock on budget sessions; tool-cap repark; `EndAdvance` on clear |
| DI | `IIdleParkedWorkBudget` → `FileIdleParkedWorkBudget` in Companion services |
| `/health`, `/settings/identity` | `idleParkedBudget` snapshot for FED Presence / Identity |
| Tests | `Prop188IdleParkedBudgetTests` — advances cap, tool-time cap, serial, leisure exemption, day rollover, persist |

### Snapshot shape (FED)

```json
{
  "localDay": "2026-11-19",
  "advancesUsed": 1,
  "advancesCap": 3,
  "toolMinutesUsed": 10.0,
  "toolMinutesCap": 45.0,
  "exhausted": false,
  "activeTaskId": null,
  "exhaustReason": null
}
```

## Evidence

```
dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests"
Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 62 ms
```

Cases: advances cap (3 then block), tool-time cap (45 min), serial one-active, exempt kinds (leisure/journal/activity-log), leisure no-op without advance, local-day rollover reset, file persist reload.

## Out of scope (unchanged)

- PROP-18.7 residue writers (activities log / journal depth)
- FED Presence HUD polish (18.3) — consumes `idleParkedBudget` when ready
- Voice wake / outbound ping (Q4/Q1 closed)

## Handoff

PM may archive PROP-18.8 on accept. FED-18.3 can read `idleParkedBudget` from `/health` or `/settings/identity` for Presence honesty.
