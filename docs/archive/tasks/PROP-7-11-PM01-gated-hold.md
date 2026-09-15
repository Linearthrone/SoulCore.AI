---
type: pm-gate-hold
prop_range: PROP-7..PROP-11
from: PM-01
to: BED-01
status: Discharged — gates cleared; shipped without PROP-N.M tickets (approved exception)
created: 2026-09-05
discharged: 2026-09-15
title: Wipeout Wave-After holds — discharged
scoreboard: docs/agents/reports/PROP-5-11-TINA-wipeout-final.md
program_accept: docs/archive/tasks/PROP-5-11-PM01-program-accept.md
registry: docs/agents/PROP_NUMBERING.md
---

# PROP-7..11 — gated hold, discharged 2026-09-15

## Original hold (2026-09-05)

| PROP | Role when released | Gate |
| --- | --- | --- |
| PROP-7 | BED | PROP-5 Pass |
| PROP-11 | BED (+ QA soak) | PROP-5 Pass |
| PROP-9 | BED | PROP-5 + PROP-7 Pass |
| PROP-10 | BED | PROP-7 Pass |
| PROP-8 | BED + QA | PROP-5 Pass; **prefer PROP-9 Pass first** |

Hold text: *"PM will mint `PROP-N.M` execution tickets only when the gate clears."*

## Discharge

PROP-5 passed (5.1–5.4 Accepted 2026-09-05, QA soak Pass), which cleared the PROP-7/11 gates;
PROP-7 Pass then cleared PROP-9/10; PROP-9 Pass cleared PROP-8. All five shipped and are on `main`.

| PROP | Gate state | Report (verdict) | Landed on `main` — evidence |
| --- | --- | --- | --- |
| PROP-7 | Cleared by PROP-5 Pass | `docs/agents/reports/PROP-7.1-BED01-to-PM01.md` (Pass) | No live Hermes client/DI in `SoulCore.Host/` |
| PROP-11 | Cleared by PROP-5 Pass | `docs/agents/reports/PROP-11.1-BED01-to-PM01.md` (Pass) | `SoulCore/SoulCore.Memory/Repositories/` (5 repos) |
| PROP-9 | Cleared by PROP-5 + PROP-7 Pass | `docs/agents/reports/PROP-9.1-BED01-to-PM01.md` (Pass) | `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/` (5 `Add*` modules) |
| PROP-10 | Cleared by PROP-7 Pass | `docs/agents/reports/PROP-10.1-BED01-to-PM01.md` (Pass) | `SoulCore.Inference/Clients/` + `Tooling/` split (landed via PR #86) |
| PROP-8 | Cleared by PROP-5 + PROP-9 Pass | `docs/agents/reports/PROP-8.1-BED01-to-PM01.md` (Pass) | `SoulCore.Host/Ws/ChatSendHandler.cs`, `ChatContextBuilder.cs`, `ChatPostEffectsHandler.cs` |

PROP-7 residue left deliberately (not a gate): `SecretNames.HermesApiKey`, `appsettings` `_note`
strings, and the `IHermesMcpInvoker` seam. The **live** contracts/config/DI the hold cared about are gone.

## Approved exception — no `PROP-7.1`..`PROP-11.1` tickets were minted

The hold promised `PROP-N.M` execution tickets at gate clear. That did not happen: BED-01 executed
each gate directly off the accepted proposal + intake and filed `PROP-{N}.1-BED01-to-PM01.md` reports.

**Recorded as the approved exception, not as missing paperwork.** Rationale:

1. Each PROP-7..11 root split 1:1 into a single work item, so a `.1` ticket would only have restated
   the proposal's acceptance criteria. `PROP_NUMBERING.md` §Rules 2 leaves `.M` division to PM.
2. Execution is fully evidenced: proposal → intake → `PROP-{N}.1` report (Pass) → code on `main`.
   The scoreboard `docs/agents/reports/PROP-5-11-TINA-wipeout-final.md` is the program-level accept.
3. Minting back-dated "Completed" ticket stubs now would add five files that were never used to
   direct work — paperwork describing paperwork, and a worse audit trail than the reports.

**Precedent going forward:** a gated hold may be discharged report-only when a PROP root has exactly
one split and a Pass report exists. Multi-split roots (PROP-1, PROP-2, PROP-4, PROP-5) still get real
`PROP-N.M` tickets before execution — see `docs/archive/tasks/PROP-5.1-PM01-to-BED01.md` …
`PROP-5.4-PM01-to-QA01.md`.

## Closure

Nothing remains gated under this hold. PROP-7..11 are `Pass` in `docs/agents/PROP_NUMBERING.md`.
Intakes and this hold are archived to `docs/archive/tasks/`; proposals to `docs/archive/proposals/`.
