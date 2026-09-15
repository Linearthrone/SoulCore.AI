---
type: pm-program-accept
prop_range: PROP-5..PROP-11
from: PM-01
to: TT-01
cc: user
status: Closed — accepted 2026-09-05; program complete, PROP-5..11 all Pass and on main
archived: 2026-09-15
created: 2026-09-05
environment: TINA-main
title: "[TINA] Accept architecture-eval wipeout — Wave NOW staffing"
cluster_map: docs/archive/proposals/architecture-eval-backlog-cluster-map.md
program_intake: docs/archive/tasks/PROP-5-11-TT01-to-PM01-program.md
scoreboard: docs/agents/reports/PROP-5-11-TINA-wipeout-final.md
gated_hold_discharge: docs/archive/tasks/PROP-7-11-PM01-gated-hold.md
---

# PROP-5..11 Accepted — TINA program control

**Decision:** Accept TT-01 recommended routes and cluster fences as written.  
**Mode:** TINA runs multi-team parallel; ≤1 Host-editing PR at a time.

## Wave NOW (5 seats — ticketed / reaffirmed)

| Seat | Lane | Ticket(s) | Owner |
| --- | --- | --- | --- |
| **A Persist** | Host SQLite + charter + SoulLoop | `PROP-5.1`…`5.3` BED → then `5.4` QA | BED-01 (sole Host) |
| **B Desktop** | Drag async delay | `PROP-6.1` BED | BED-01 (Inference only) |
| **C SMS** | DIGITS security / QA / Link shrink | `PROP-1.4` SEC · `1.5` QA · `1.6` FED (after 1.5) | SEC / QA / FED |
| **D UE** | Kayleigh possess reliability | `PROP-2.1`–`2.4` REX | REX-01 |
| **E Presence** | House drawer + installer | `PROP-4.1` FED · `PROP-4.2` OPS | FED / OPS |

## Gated (accepted, not started)

| PROP | Gate |
| --- | --- |
| PROP-7 Hermes dead surface | After PROP-5 Pass |
| PROP-11 Memory repo split | After PROP-5 Pass (may || PROP-7) |
| PROP-9 Host DI modules | After PROP-5 + PROP-7 |
| PROP-10 Inference clients/tools | After PROP-7 (may || PROP-9/11) |
| PROP-8 Chat orchestration | After PROP-5; **prefer after PROP-9** |

## Hard fences (kill if violated)

- Two open PRs both edit `Program.cs` or `ChatWebSocketHandler`
- Hitchhike EF / second DB onto PROP-5
- Absorb PROP-1..4 into wipeout
- Mint IMAP pool / vector / docs-merge without measured pain

## Parked (no ticket)

IMAP pool · full docs-site merge · standalone vector · Unreal adapter (stays under PROP-2)

TT-01: program accepted. Further TT only on unblock evals.

## Closeout (2026-09-15 registry reconcile)

The "Gated (accepted, not started)" table above is resolved — all five gates cleared and all five
shipped. They were executed **without** `PROP-7.1`..`PROP-11.1` tickets; that is recorded as the
approved exception in `docs/archive/tasks/PROP-7-11-PM01-gated-hold.md`.

Seats A and B closed (`PROP-5.1`–`5.4`, `PROP-6.1` all Completed, archived beside this file).
Seats C, D, E are **still open** and stay in `docs/agents/tasks/`:

| Seat | Open items |
| --- | --- |
| C SMS | `PROP-1.5` QA (Pending), `PROP-1.6` FED (Pending). `PROP-1.4` SEC = Completed — Partial |
| D UE | `PROP-2.1`–`2.3` Pending, `PROP-2.4` Held |
| E Presence | `PROP-4.1` Partial, `PROP-4.2` Pending |
