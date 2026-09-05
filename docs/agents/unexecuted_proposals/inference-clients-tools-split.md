---
type: proposal
prop_id: PROP-10-inference-clients-tools-split
status: sent-to-pm
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Inference clients vs tools — module boundary cleanup
need: Stop SoulCore.Inference from being both inference adapter and broad application-services bucket — clear Clients vs Tooling vs capability folders
parallel_with: PROP-9, PROP-11, PROP-1/2/4
blocked_by: PROP-7 (HermesToolRouting / dead Hermes refs)
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
sent_at: 2026-09-05
pm_intake: docs/agents/tasks/PROP-10-TT01-to-PM01.md
---

# Inference clients vs tools split

## 1. Need / Want

`SoulCore.Inference` holds Ollama clients, null clients, registry abstractions, and 100+ tool files, and references Memory/Config/Adapters. Kurt wants a boundary where “talk to the model” and “run a capability tool” are not one mental blob — without a premature multi-repo explosion.

## 2. Goal & Success Criteria

- Clear folders (and optionally projects): `Clients/` (Ollama/embeddings/nulls), `Tooling/` (registry/loop helpers), capability tools remain under `Tools/*`.
- Project references justified in csproj comments; no new Host behavior.
- Build + Inference/Protocol tests green.
- Does not move ChatWebSocketHandler; does not change SQLite.

## 3. Context & Constraints

- After PROP-7 so Hermes routing files are gone.
- Avoid `Program.cs` churn — if registration paths change, coordinate as follow slice under PROP-9 or a tiny allowlisted edit after PROP-9.
- PROP-6 owns Desktop Sleep — do not retouch drag timing here.

## 4. Clarifying Q&A

| Q | Default |
| --- | --- |
| Separate NuGet/projects now? | Prefer folders first; split projects only if reference graph demands |
| Move tools to SoulCore.Tools? | Optional second slice — not required for Pass |

## 5. Avenues Explored

- **A (recommended):** In-project folder boundary + csproj cleanup.
- **B:** New `SoulCore.Tools` project — later if A still leaves reference pain.
- **C:** One tool assembly per capability — rejected as first move.

## 6. Recommended Route

Avenue A after PROP-7. Parallel OK with PROP-9/11 if Host/Memory files untouched.

## 7. Alternatives (parked)

Per-capability assemblies; moving tool-loop into Host.

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Massive churn / merge hell | Folder moves in one PR with namespace updates only |
| Touches Program.cs heavily | Defer registration renames to PROP-9 |

## 9. Open Questions

Does Kurt want a literal second csproj this wave, or folder hygiene Pass?

## 10. Suggested PM Handoff

- `prop_id`: `PROP-10-inference-clients-tools-split`
- `PROP-10.1` BED — folder/clients vs tooling boundary
- `PROP-10.2` BED (optional) — `SoulCore.Tools` project extract
