---
type: proposal
status: sent-to-pm
tt_id: TT-01
created: 2026-07-22
updated: 2026-07-22
title: PM-01 cold-start first briefing — House Victoria AI layer
need: Cold-start the House Victoria AI layer — assess documented state, recommend highest-leverage next priorities, suggest initial ticketed work sequence for specialized agents
sent_at: 2026-07-22
pm_intake: docs/agents/tasks/TASK-20260722-001-TT01-to-PM01.md
---

# PM-01 Cold-Start First Briefing — House Victoria AI Layer

## 1. Need / Want

PM-01 activated on a cold-start and requested a **first briefing**: assess current documented state of House Victoria AI, recommend highest-leverage next execution priorities, and suggest an initial ticketed work sequence for FED / BED / DBD / SEC / OPS / QA / SLOP.

## 2. Goal & Success Criteria

| Goal | Success looks like |
| --- | --- |
| Honest status | Progress claims reconciled with disk evidence (this workspace + named product root) |
| Executable control plane | Canonical agent paths, role files, empty→usable queue conventions |
| Safe sequencing | No Phase 3 / write tickets until product ground truth + security/ops gates |
| Actionable handoff | PM can issue 3–6 concrete tickets without inventing TASK-006–008 or fabricating Pass |

## 3. Context & Constraints

### Verified workspace facts (2026-07-22)

| Fact | Evidence |
| --- | --- |
| Soul_Core is **control-plane scaffolding only** | Tree = `Agents/*.md` + `docs/agents/{tasks,reports,log,issues}/` (empty) + `unexecuted_proposals/README.md` |
| **No product code** | No FastAPI/Nuxt/`tmpa.py`/`skills/`/`ops/`/`HouseVictoria.App` |
| **No TASK-\*** files | `tasks/` and `log/` empty — TASK-006 / 007 / 008 do **not** exist |
| Role pack under `Agents/` | PM/TT/FED/BED/DBD/SEC/QA/SLOP present; **OPS-01.md missing** (only `OPS-01-EN.md`); no `DEV-01.md` |
| Path dualism | PM/QA/FED/BED docs still point at `Docs/agents/agents/` — that tree **does not exist** |
| Collaboration dirs exist | Created and empty — ready for first real tickets |

### Documented intent (PM-01.md — treat as narrative, not verified status)

- Stack: Nuxt2/Vue2 + FastAPI + MariaDB read-only; TMPA for AI data; locked versions
- Claimed complete: Phase 1 scan, Phase 2 NL2SQL + TMPA + multi-window collab scaffolding
- Claimed in progress: TMPA v3.1 docs (006), OPS TMPA docs (007), QA docs/regression (008) — **stale here**
- Planned Phase 3: Guardian / Specialist / Analyst / Executor / Auditor / Conductor
- Iron rules: no version upgrades; AI layer no DB (TMPA); no fabricated data; atomic writes; prefer specialized agents

### Iron rules (binding when code appears)

1. No version upgrades (Java 8 / Node 14 / Python 3.10)
2. AI layer uses no database — TMPA file storage only
3. LLM must not fabricate business data
4. Atomic writes via `tmpa.py` only
5. Prefer FED/BED/DBD/SEC over DEV-01

## 4. Clarifying Q&A (answered)

| Q | A |
| --- | --- |
| Stuck ticket? | No — cold-start idea mode |
| Send to PM? | Yes — PM explicitly requested first briefing |
| Ticket FED/BED from TT? | No — suggest only; PM owns execution tickets |

## 5. Avenues Explored

Thinktank seats: **STRAT**, **CONTRA**, **SYS**, **RISK** (parallel). Strong consensus; dissent only on A vs B packaging (control-plane-only vs import code), not on “no Phase 3 yet.”

### Avenue A — Bootstrap control plane + ground truth first *(recommended)*

1. PM hygiene: invalidate stale §5.2; fix path canon to `Agents/`; OPS CN stub or canonicalize EN
2. Locate / attach product root (user decision)
3. OPS inventory (deploy/health/access)
4. BED inventory (orchestrator / TMPA / skills vs handbook map)
5. DBD only if RO DB path real; QA smoke only if URL reachable; SEC thin after OPS facts
6. Park Phase 3 until Phase 2 surface re-proven

### Avenue B — Dual-track: hygiene + mount/link external product *(acceptable if code lives elsewhere)*

Same as A, plus explicit OPS/user ticket: attach product tree (multi-root / junction / submodule) and write CODE_ROOT convention. Still block Phase 3 / revive 006–008 until attach succeeds.

### Avenue C — Jump to Phase 3 / revive TASK-006–008 *(rejected)*

Assumes Phase 2 complete and ghosts are real work. Seats agree this produces fabricated status, empty-ticket theater, and early TT unblock churn.

## 6. Recommended Route

**Adopt Avenue A (default), with Avenue B packaging if user confirms product lives outside Soul_Core.**

### Recommended first sequence (for PM ticketing — not created by TT)

| Order | Role | Suggested ticket intent |
| --- | --- | --- |
| 0 | **PM-01** (self) | Ground-truth handbook pass: mark §5.2 TASK-006–008 void/stale; declare workspace = control plane; set product root = `__TBD__`; fix `Agents/` vs `Docs/agents/agents/` pointers; add `OPS-01.md` (or alias EN) |
| 1 | **OPS-01** | Locate/attach product + env map: deploy root, process manager, health URL, reachability from this machine |
| 2 | **BED-01** | Repo reality inventory: `chat_orchestrator`, `tmpa.py`, skills layout, chat API — map vs PM §6; **gaps only, no feature build** |
| 3 | **DBD-01** | *If* MariaDB RO path real: RO posture check + schema/skills alignment note (no writes) |
| 4 | **QA-01** | Baseline smoke against documented URL/API; evidence-only; no Pass without output |
| 5 | **SEC-01** | Thin: auth/secrets/tenant isolation contract vs actual env (after OPS facts) |
| — | **FED-01 / SLOP-01** | **Defer** until UI surface or post-QA audit target exists |
| — | **Phase 3 / Executor / Conductor** | **Defer**; Executor stays disabled-by-default until dual-control + RO proof |

### Highest-leverage priorities (next 1–2 weeks)

1. **Truth over velocity** — stop treating handbook Phase 2 / TASK-006–008 as open queue
2. **Path canon** — one agent root (`Agents/`) so specialized windows do not thrash
3. **Product attachment** — agents must see real code or PM must declare greenfield
4. **Inventory before invention** — BED/OPS map reality; do not invent TMPA/skills
5. **Security gate before Phase 3a** — auth + tenant isolation contracts before Guardian tickets
6. **Hard-block Executor / Conductor** until RO DB + write allowlist + dual-control exist

### Seat synthesis (facilitator)

| Seat | 2–4 bullets |
| --- | --- |
| **STRAT** | Control plane ≠ product; stale §5.2; Phase 3 is not week-1 leverage; sequence PM→OPS→BED→(DBD)→QA→SEC |
| **CONTRA** | Kill Phase 3 / revive 006–008 without code; kill multi-role fan-out on empty dirs; path dualism is day-one failure |
| **SYS** | Prefer mount/link (A) over import (B=L); docs inventory mandatory; dependency graph [locate]→[hygiene]→[artifacts]→[runtime]→[3a] |
| **RISK** | Must-mitigate before BED/FED Phase 3a: TMPA atomic helpers, RO DB proof, auth/tenant freeze, QA TMPA cases; blast radius: Executor > Conductor > Auditor > Guardian |

**Conflicts:** None material on recommendation. A vs B is a packaging decision for the user (where code lives), not a strategy split.

## 7. Alternatives (parked)

- **Avenue C** — Phase 3 / ghost TASK revival — parked until ground truth proves Phase 2 present
- **Full monorepo import into Soul_Core** — parked until inventory; revisit if control-plane-only proves painful
- **FED-first UI polish** — parked; no UI surface in this workspace
- **DEV-01 mega-ticket** — parked; violates specialized-agent preference with no inseparable FE+BE need

## 8. Risks & Kill Criteria

| Risk | Kill criterion |
| --- | --- |
| Status hallucination from PM §5 | Kill any ticket that “continues TASK-006/007/008” or “finishes Guardian” without locating artifacts |
| Phase 3 before ground truth | Kill Guardian+ tickets if no runnable AI layer / skills / auth boundary in declared repo |
| Over-ticketizing scaffolding | Kill week-1 fan-out across >3 execution roles if product root still unknown |
| Path confusion | Kill “activate all patrols” until Required Reading matches disk |
| Iron-rule theater | Kill DoD that cites `tmpa.py` / 12 skills when those paths do not exist |
| Executor early | Kill any write-enable ticket until RO proof + allowlist + dual-control |

## 9. Open Questions for User / PM

1. **Where is the authoritative House Victoria AI product tree** (local path and/or GitLab URL)? Should Soul_Core stay control-plane-only (mount/link) or eventually hold code?
2. Is there a **reachable AI staging/prod endpoint** (URL + auth pattern) for week-1 QA/OPS, or is work inventory/docs-only until access is provisioned?
3. Should PM **explicitly retire** §5.2 TASK-006/007/008 as void template debt, or import history from another repo first?
4. Is **Executor** in scope for the next 90 days, or explicitly parked?
5. What is the **tenant isolation source of truth** today (JWT / session / DB filter), and who owns proving cross-company deny before Phase 3a?

## 10. Suggested PM Handoff

- **Likely roles (first wave):** PM self-hygiene → OPS-01 → BED-01 → (DBD-01) → QA-01 → SEC-01
- **Defer:** FED-01, SLOP-01, Phase 3 roles, DEV-01, any “TMPA v3.1 continue 006” coding
- **Suggested split / order:** See §6 table
- **What PM should decide first:**
  1. Product root location + A vs B packaging
  2. Retire vs import TASK-006–008 narrative
  3. Whether week-1 includes live endpoint smoke or inventory-only
  4. Then issue OPS + BED inventory tickets (not Phase 3)

---

*TT-01 Ready. Proposal synthesized 2026-07-22 from STRAT + CONTRA + SYS + RISK. No execution tickets filed by TT.*
