---
type: note
from: PM-01
id: TINA
role: Project Manager + Architect + Product Manager + AI-CTO
created: 2026-08-05
title: Cold-start patrol — TINA assumes PM-01 (Linux cloud)
---

# PM-01 Cold-Start Patrol (TINA) — 2026-08-05

Callsign **TINA** — *Tactical Intelligence & Navigation Architect*.  
Role pack: `Agents/PM-01.md` + `Agents/PM-01-Work-Standards.md` (+ EN twin).

## Ground truth (this workspace)

| Fact | Evidence |
| --- | --- |
| Product home | `SoulCore/` + `House/` (Avenue A — Soul-spine MVP) |
| Control plane | `docs/agents/{tasks,reports,log,issues,unexecuted_proposals}` |
| Branch | `cursor/tina-pm-cold-start-169c` off `main` @ `ec04052` |
| Host | **UP** — `GET http://127.0.0.1:7700/health` → `status=ok`, bind `127.0.0.1:7700` |
| Host knobs (this session) | `StubWhenModelDown=true`, `Hermes=false`, `Inference=false`, `SoulLoop=false` |
| Ollama | Binary present; **no running instance / no models listed** |
| Unreal / LLMOD quarry | **Not** in this Linux cloud tree — UE tickets cannot execute here |
| Shadow MT4/MCP | Out of band — OPS-170 Fail (Kurt on `house-victoria`) |
| Cloud DB charter | `charter.mode=empty` (local SQLite fresh; Kurt’s locked DB is not this VM) |
| `/health` tools note | Live snapshot shows `mt4Backend=hermes` under stub session; code default remains `llmod` → `house-victoria:8080` |

## Queue snapshot (open `tasks/`)

| Ticket | Role | Report | PM decision |
| --- | --- | --- | --- |
| BED-121 | BED-01 | Partial (montages done; AC-3 was AnimBP) | **Hold** — re-probe when UE/PIE available (115 DefaultSlot claimed unblock) |
| QA-123 | QA-01 | none | **Hold** — needs BED-121 Pass + UE visual |
| QA-134 | QA-01 | SOFT-PASS | **Accept soft** — formal Phase B exit still needs tool-capable model + path-follow/UE as gated |
| QA-137 | QA-01 | none | **Hold** — desktop/browser + Hermes; ISSUE-008 capture timeout open |
| QA-139 | QA-01 | none | **Hold** — blocked on OPS-170 live MT4 edge |
| BED-160 | BED-01 | none | **Hold** — ISSUE-006 PIE travel=0; UE on shadow |
| SLOP-160 | SLOP-01 | findings (5) | **Archived** with BED-172 Pass; F4 ask-user still open |
| BED-172 | BED-01 | Pass | **Archived** — code on `cursor/bed-172-slop-cleanup-169c` |
| BED-169 | BED-01 | Partial (code Pass) | **Accept code** — live verify after shadow MCP |
| OPS-170 | OPS-01 | Fail | **Accept Fail** — Kurt must start MCP `:8080` + EA on `house-victoria` |
| BED-171 | BED-01 | Partial | **Accept Partial** — sync `VictoriaBody` asset then run rewire scripts |

## User blockers (need Kurt)

1. **OPS-170 / MT4 edge** — On shadow `house-victoria` (`100.107.94.17`): MT4 + EA + LLMOD MCP HTTP bind `0.0.0.0:8080`. See OPS-170 report manual steps.
2. **BED-171 / VictoriaBody** — Sync MetaHuman `VictoriaBody` into MyProject (main or shadow), then run BED-171 Python rewire.
3. **BED-160 / QA-118** — Open MyProject PIE on shadow body WS; fix travel=0.
4. **Soak #2** — still an open user gate (PRODUCT_ROOT).
5. **Ollama model for formal QA-134** — pull a tool-capable model (product baseline `gemma4:latest` or gate model `qwen2.5:14b`) when ready for formal agency exit.

## This patrol — actions

| Action | Status |
| --- | --- |
| Assume TINA / PM-01 | Done |
| Bring Host up (stub LLM) | Done — health ok |
| Accept BED-169 / OPS-170 / BED-171 / QA-134 soft | Done (status notes on tickets) |
| Hand off SLOP-160 | Done — findings report landed |
| Ticket BED-172 cleanup | Done — dispatched same turn |
| New code tickets | BED-172 (cloud-executable hygiene) |

## Recommended priority

1. **Kurt (shadow):** OPS-170 MCP+EA → unblocks QA-139 live path  
2. **SLOP-160** (this cloud): finish Phase E hygiene chain  
3. **Kurt (UE):** VictoriaBody sync (171) → BED-160 / QA-118 → BED-121 re-probe → QA-123  
4. **Model pull** → formal QA-134 re-run  
5. User authorize soak #2 when SoulLoop+embeddings desired on a durable Host
