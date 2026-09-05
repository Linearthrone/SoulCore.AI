---
type: proposal
prop_id: PROP-9-host-di-composition-modules
status: sent-to-pm
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Host DI composition modules — extract registrations from Program.cs
need: Keep Program.cs as composition root without a 1.1k-line kitchen sink — group AddMemory/AddInference/AddTools/AddCompanion/AddVoice/AddWebEndpoints
parallel_with: PROP-10, PROP-11, PROP-1/2/4
blocked_by: PROP-5, PROP-7
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
sent_at: 2026-09-05
pm_intake: docs/agents/tasks/PROP-9-TT01-to-PM01.md
---

# Host DI composition modules

## 1. Need / Want

`Program.cs` mixes config, CLI evidence modes, all DI registrations, middleware, and endpoints (~1237 lines). Kurt wants dependency groups auditable so accidental cross-layer wiring is visible — without changing runtime behavior.

## 2. Goal & Success Criteria

- Extension methods (or equivalent) per area: Memory, Inference, Tools, Companion/Presence hooks, Voice, Web endpoints.
- `Program.cs` becomes order + call list + minimal host bootstrap.
- Behavior parity: same service lifetimes; boot + `/health` + WS smoke green.
- No feature work hitchhiked.

## 3. Context & Constraints

- **After PROP-5** (charter ownership edits settle) and **after PROP-7** (Hermes binds gone) so modules are not extracting dead code.
- Sole Host lane while open — do not parallel with PROP-8.

## 4. Clarifying Q&A

| Q | Default |
| --- | --- |
| Move middleware to modules too? | Yes if it clarifies; keep endpoint map readable |
| Change lifetimes? | No — move only |

## 5. Avenues Explored

- **A (recommended):** `Hosting/ServiceCollectionExtensions/*.cs` modules.
- **B:** Separate assemblies per area — parked (too heavy).
- **C:** Autofac modules — rejected (new DI stack).

## 6. Recommended Route

Avenue A. One BED seat. Pure move/refactor.

## 7. Alternatives (parked)

Multi-assembly Host; new DI container.

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Registration order bugs | Keep explicit call order; smoke tests |
| Parallel with PROP-8 | Forbidden |

## 9. Open Questions

None blocking.

## 10. Suggested PM Handoff

- `prop_id`: `PROP-9-host-di-composition-modules`
- `PROP-9.1` BED — extract modules + parity smoke
- PM decides first: run before or after PROP-8 (TT prefers **before**)
