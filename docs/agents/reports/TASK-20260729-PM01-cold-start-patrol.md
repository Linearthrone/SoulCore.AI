---
type: note
from: PM-01
id: TINA
role: Project Manager + Architect + Product Manager + AI-CTO
created: 2026-07-29
title: Cold-start patrol — TINA assumes PM-01
---

# PM-01 Cold-Start Patrol (TINA)

Callsign **TINA** — *Tactical Intelligence & Navigation Architect*. Role pack: `Agents/PM-01.md` + `PM-01-Work-Standards.md`.

## Ground truth (this workspace)

| Fact | Evidence |
| --- | --- |
| Product home | `SoulCore/` + `House/` (Avenue A — Soul-spine MVP) |
| Control plane | `docs/agents/{tasks,reports,log,issues,unexecuted_proposals}` |
| Active queue | 18 tickets in `tasks/` (116–145 Wave 26/27 + 154 phone); 121 report Partial in `reports/` |
| Highest archived IDs | through TASK-156 (2026-07-27) |
| Host / Ollama | Host not answering `:7700`; Ollama binary present, **no models listed** |
| Unreal / LLMOD quarry | **Not** in this Linux cloud tree — Wave 26 UE tickets cannot execute here |
| Phone QA-154 | No `adb` — device/emulator gate deferred |

## Wave status (from PRODUCT_ROOT + log)

- Waves 14–25 / Phase A agency (125–133, 156) / Wave 28 FED+SEC+OPS (147–153, 155): **archived Pass**
- Wave 26 Phase 1: BED-114/115 Pass; **116→117→118** still open (NavMesh / path-follow / visual walk) — **UE-bound**
- Wave 27 Phase 2: 119/120/122 Pass; **121 Partial** (montages done; AC-3 was AnimBP — **115 DefaultSlot clears blocker** → re-probe + QA-123 when UE available)
- Wave 27 Phase 3: Phases C–F (135–145) gated on **OPS-143**; Phase E (**140–142**) **not** Hermes-gated
- Wave 28: QA-154 exit gate still Pending

## Open issues

| Issue | Status | Next |
| --- | --- | --- |
| ISSUE-20260726-002 `source='model'` | Open P2 | **DBD-157** dispatched |
| ISSUE-20260727-003 `move_to` path-follow | Open | Blocked on BED-117 (UE) |
| ISSUE-20260727-001 ListTools DI | Fixed | — |

## This patrol — dispatches

| Ticket | Role | Why now |
| --- | --- | --- |
| BED-140 | BED-01 | Phase E task tools — pure C#, no Hermes/UE |
| OPS-143 | OPS-01 | Hermes restore — unblocks C/D/F |
| DBD-157 | DBD-01 | ISSUE-002 migration 003 |
| QA-134 | QA-01 | Soft agency gate (dispatch logs OK without path-follow) |

**Held (environment):** 116/117/118/121-reprobe/123/154 — need Windows UE and/or Android.

**Migration ID lock:** DBD-157 = `003_*`; BED-140 = `004_victoria_tasks.sql`.

## Recommended priority after this wave

1. Land BED-140 → hand off BED-141 + QA-142  
2. Land OPS-143 → unlock BED-135/136/138 (then QA-137/139) + BED-144/QA-145  
3. When Kurt’s UE machine is available: BED-116 → 117 → QA-118; then QA-123  
4. Charter lock + soak #2 remain **user gates**
