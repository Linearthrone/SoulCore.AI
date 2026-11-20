---
type: report
from: SLOP-01
to: PM-01
task_id: PROP-18.14
prop_id: PROP-18.14
prop_root: PROP-18-chronology-and-felt-persistence
status: findings
created: 2026-11-19
related_qa: docs/agents/reports/PROP-18.11-QA01-to-PM01.md
related_bed: docs/agents/reports/PROP-18.8-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-18.14 (PROP-18.8 idle B3 budget surface)

## Scope

- Related QA: `docs/agents/reports/PROP-18.11-QA01-to-PM01.md` (Pass)
- Related BED: `docs/agents/reports/PROP-18.8-BED01-to-PM01.md` (Pass)
- Files / packages scanned:
  - `SoulCore/SoulCore.Inference/Presence/IdleParkedWorkBudget.cs` (`IIdleParkedWorkBudget`, `FileIdleParkedWorkBudget`, snapshot, `IsExemptKind`)
  - `SoulCore/SoulCore.Host/Loop/AssignedWorkHostedService.cs` (`CanStartAdvance` gate)
  - `SoulCore/SoulCore.Host/Loop/AssignedWorkRunner.cs` (`TryBeginAdvance` / `RecordToolTime` / `IsToolTimeExhausted` / `EndAdvance` / `_budgetSession`)
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (`idleParkedBudget` on `/health` + `/settings/identity`, `IdleParkedBudgetDto`, `SafeParkedBudgetSnapshot`)
  - `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/CompanionServiceCollectionExtensions.cs` (DI)
  - Adjacent non-consumers: `LeisureSession.cs`, `ActivityResidueWriter.cs` (confirm no budget calls)
  - Tests: `Prop188IdleParkedBudgetTests.cs`
- Method: read primary sources + ripgrep for `IIdleParkedWorkBudget` / `IdleParkedBudgetDto` / `IsExemptKind` / `idleParkedBudget` / `TryBeginAdvance` / `RecordToolTime` / `SafeParkedBudgetSnapshot`
- **Read-only.** No code modified.

## Summary

- Findings count: **2**
- Highest severity: **P2**
- Verdict: **findings**

## Findings

### F1 — `/health` reinvents safe snapshot try/catch instead of `SafeParkedBudgetSnapshot`

- Category: duplicate
- Severity: **P2**
- Evidence:
  - Identity: `IdleParkedBudgetDto(SafeParkedBudgetSnapshot(parkedBudget))` (`WebApplicationExtensions.cs` ~992, helper ~1384–1394).
  - Health: inline `try { IdleParkedBudgetDto(parkedBudget.GetSnapshot()); } catch { IdleParkedBudgetDto(null); }` (~151–158) — same failure→null-DTO path, second shape.
- Why it matters: one shared exposure helper already exists for the B3 DTO; a second defensive shape can drift (e.g. health logs vs identity swallow) when snapshot semantics change.
- Recommended action: **dedupe**
- Suggested owner: **BED-01**
- Ask-user question: (n/a)

### F2 — `IsExemptKind` is unused on the product path

- Category: slop (dead export / unused helper in change surface)
- Severity: **P2**
- Evidence:
  - `FileIdleParkedWorkBudget.IsExemptKind` defined in `IdleParkedWorkBudget.cs` ~59–68 (leisure / journal / activity-log|activity_log / soulloop|soul-loop).
  - Ripgrep under `SoulCore/**/*.cs`: only `Prop188IdleParkedBudgetTests` calls it; Host / Leisure / residue writers never gate on it.
  - Real exemption is by non-call: `LeisureSession` has 0 budget references; `RecordToolTime` no-ops without `_activeTaskId`.
- Why it matters: public helper looks like a call-site gate but nothing enforces via it — future writers may assume kinds are checked, or the helper becomes drift vs actual callers.
- Recommended action: **ask-user**
- Suggested owner if remove/dedupe: **BED-01**
- Ask-user question: Keep `IsExemptKind` as the intentional Q3 law catalog (tests + future FED/residue), or remove / internalize it and rely solely on “never call the budget API” for exempt surfaces?

## Clean areas

- **Single budget implementation:** one `IIdleParkedWorkBudget` → `FileIdleParkedWorkBudget`; no parallel idle/agency budget types.
- **Single snapshot DTO mapper:** `IdleParkedBudgetDto` shared for `/health` and `/settings/identity` (only the safe-get path duplicates — F1).
- **Single DI registration:** Companion services singleton only.
- **Serial + tool-time path coherent:** HostedService pre-filter (`CanStartAdvance`) + Runner consume (`TryBeginAdvance` / `_budgetSession` / `RecordToolTime` / mid-session `IsToolTimeExhausted` / `EndAdvance` on clear) — layered gates, not alias sprawl.
- **Leisure / residue isolation:** no accidental budget coupling on leisure or residue writers.
- **Exempt spelling variants** inside `IsExemptKind` (`activity-log`/`activity_log`, `soulloop`/`soul-loop`) are input aliases for one purpose — not same-purpose helper sprawl.
- **File Load/Persist empty catches** match Host “must not block start / work loop” convention; not abnormal defensive noise for this surface.
- **PROP ticket comments** on budget gates are local convention, not restated-noise findings.

## Notes for PM

- Safe to archive? **yes** for the functional PROP-18.8 → 18.11 → 18.14 chain — both findings are **P2**; B3 behavior/exposure is not blocked.
- Optional: ticket BED-01 for **F1** (one-line: health → `SafeParkedBudgetSnapshot`) if you want hygiene before FED-18.3 Presence HUD consumes the field heavily.
- **F2** needs LinearThrone / PM disposition (keep law helper vs remove); defer without blocking “done.”
- ISSUE-20261119-001 (ActivityResidueComposer CS1061) is out of this SLOP scope (build gate); already tracked by QA.
- No re-ticket SLOP unless BED cleanup grows beyond F1/F2.
