---
type: proposal
prop_id: PROP-11-memory-store-repository-split
status: sent-to-pm
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Memory store repository split — after concurrency is safe
need: Split SqliteMemoryStore god-object into repositories behind existing interfaces once connection ownership/serialization is proven
parallel_with: PROP-7 (Host Hermes), PROP-10 (Inference folders), PROP-1/2/4
blocked_by: PROP-5
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
sent_at: 2026-09-05
pm_intake: docs/agents/tasks/PROP-11-TT01-to-PM01.md
---

# Memory store repository split

## 1. Need / Want

After PROP-5 makes SQLite access safe, `SqliteMemoryStore` still concentrates memory, emotion, stats, tasks, workflows, journals, and disposal. Kurt wants schema ownership and change coupling reduced — without inventing a second database.

## 2. Goal & Success Criteria

- Repositories (or partial classes → types) behind existing interfaces: episodic memory, emotion, tasks, workflows, journals (+ stats as needed).
- Shared DB factory / session abstraction consistent with PROP-5 connection policy.
- Host DI still composes implementations; public interfaces stable unless PM approves breaks.
- Concurrent soak from PROP-5 still green.
- **Vector/indexed recall** is an **optional later slice** only with a measured recall cost failure — not part of MVP Pass.

## 3. Context & Constraints

- Hard gate: PROP-5 Pass (serialize/factory + charter ownership).
- Do not edit ChatWebSocketHandler (PROP-8) or Program modules beyond registration lines for new types (prefer PROP-9 in place).
- One SQLite file remains.

## 4. Clarifying Q&A

| Q | Default |
| --- | --- |
| Include vector index in MVP? | **No** |
| Change table schemas? | No — extract only |

## 5. Avenues Explored

- **A (recommended):** Extract repos sharing PROP-5 session/gate; keep one file.
- **B:** Multiple SQLite files per concern — rejected (ops ceremony).
- **C:** EF Core rewrite — rejected.

## 6. Recommended Route

Avenue A after PROP-5. Parallel with PROP-7/10 if file fences held.

## 7. Alternatives (parked)

EF; multi-DB; standalone vector PROP.

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Extract before concurrency fix | Blocked by PROP-5 |
| Interface break churn across Host | Preserve interfaces; adapter concrete |
| Vector scope creep | Kill MVP if vector starts without metric |

## 9. Open Questions

Any interface renames Kurt wants while the patient is open? (Default: no.)

## 10. Suggested PM Handoff

- `prop_id`: `PROP-11-memory-store-repository-split`
- `PROP-11.1` BED — session factory alignment with PROP-5 + repo extract
- `PROP-11.2` QA — soak + interface parity tests
- `PROP-11.3` (optional later) BED — indexed/vector recall only with metric
