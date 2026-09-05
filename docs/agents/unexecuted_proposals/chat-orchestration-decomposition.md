---
type: proposal
prop_id: PROP-8-chat-orchestration-decomposition
status: unexecuted
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Chat orchestration decomposition — handler, prompt builder, history, gated parallel reads
need: Break ChatWebSocketHandler (~1.6k lines) into testable command/session pieces; one prompt owner; bounded history; parallel context reads only after SQLite is safe
parallel_with: PROP-10 (Inference-only), PROP-1/2/4
blocked_by: PROP-5; prefer also after PROP-9
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
---

# Chat orchestration decomposition

## 1. Need / Want

`ChatWebSocketHandler` owns socket lifecycle, frame routing, chat orchestration, emotion, memory authoring, tool-loop, and Unreal side effects. Kurt needs this boundary decomposable so chat behavior can change without 17-field constructor fear — and context assembly should stop being six sequential string patches plus a front-trimmed `List<T>`.

## 2. Goal & Success Criteria

- Session runner + focused handlers (chat / emotion / loop-tick ack / post-chat effects) with thinner DI surfaces.
- One `ChatContextBuilder` (or equivalent) owns prompt section order.
- `ChatSessionHistoryStore` uses deque/ring + copy-on-read snapshot (no `RemoveAt(0)` loop).
- After PROP-5 Pass: coordinated parallel reads for independent context pieces returning one immutable context object.
- Existing tool-loop / WS tests stay green; add focused tests per extracted type.
- **Does not** extract Program.cs modules (PROP-9) or split Memory repos (PROP-11).

## 3. Context & Constraints

- Hottest Host file after Program.cs — **sole Host lane** while open.
- Prefer after PROP-9 so DI modules exist for new types; acceptable after PROP-7 if PROP-9 deferred.
- Parallel reads are **blocked** on PROP-5; ship structural split first if needed, enable parallel reads as last slice.

## 4. Clarifying Q&A

| Q | Default |
| --- | --- |
| Big-bang rewrite vs strangler extracts? | Strangler — extract by seam, keep behavior |
| Include tool-loop engine move? | Only if required to thin handler; else leave |

## 5. Avenues Explored

- **A (recommended):** Strangler: ContextBuilder + History store fix + command handlers; then parallel reads.
- **B:** Rewrite new WS stack — rejected (regression magnet).
- **C:** Only prompt builder, leave god-handler — rejected (insufficient).

## 6. Recommended Route

Avenue A. One BED team. Fold eval Better-if items (prompt, history, parallel reads) here — **do not** mint three PROPs.

## 7. Alternatives (parked)

Full Inference-side chat engine; SignalR migration.

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Behavior drift in tool-loop | Characterization tests before extract |
| Parallel reads before PROP-5 | Hard gate in acceptance |
| Two teams editing handler | Kill program criterion |

## 9. Open Questions

Prefer PROP-9 before this, or accept registering new types in Program.cs temporarily?

## 10. Suggested PM Handoff

- `prop_id`: `PROP-8-chat-orchestration-decomposition`
- `PROP-8.1` BED — history deque/ring
- `PROP-8.2` BED — ChatContextBuilder
- `PROP-8.3` BED — strangler handlers / session runner
- `PROP-8.4` BED — parallel context reads (after PROP-5 evidence)
- `PROP-8.5` QA — chat+tool-loop regression soak
