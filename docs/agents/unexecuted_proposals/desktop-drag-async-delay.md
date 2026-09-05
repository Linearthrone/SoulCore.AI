---
type: proposal
prop_id: PROP-6-desktop-drag-async-delay
status: unexecuted
tt_id: TT-01
created: 2026-09-05
updated: 2026-09-05
title: Desktop drag — replace Thread.Sleep with async delay
need: Stop NativeDesktopControlBackend drag interpolation from blocking a thread ~300ms via Thread.Sleep while pretending to be async
parallel_with: PROP-1, PROP-2, PROP-4, PROP-5
blocked_by: none
cluster_map: docs/agents/unexecuted_proposals/architecture-eval-backlog-cluster-map.md
---

# Desktop drag — async delay

## 1. Need / Want

Desktop drag paths use `Thread.Sleep(15)` inside methods that return `Task`, blocking a thread for roughly 300ms per drag. Kurt needs tool calls to stay async-honest so Host threads are not pinned during pointer interpolation.

## 2. Goal & Success Criteria

- No `Thread.Sleep` on the desktop drag interpolation path.
- Delay uses `Task.Delay` (or equivalent) with cancellation honored.
- Existing desktop tool tests still pass; add/extend a test that drag does not block synchronously for the full interpolate window.
- **No** Host/`Program.cs` / Memory / Hermes edits.

## 3. Context & Constraints

- File ownership: `SoulCore.Inference/Tools/Desktop/NativeDesktopControlBackend.cs` (+ desktop tool tests only).
- Parallel-safe with PROP-5 (different project surface).
- Keep scoped desktop gate behavior unchanged.

## 4. Clarifying Q&A

| Q | Default |
| --- | --- |
| Change click/move paths too if they sleep? | Yes, any Sleep in this backend |
| Redesign drag algorithm? | No — delay only |

## 5. Avenues Explored

- **A (recommended):** `await Task.Delay(…, ct)` in interpolate loop.
- **B:** Dedicated input worker thread — parked (overkill).
- **C:** Remove interpolation — parked (behavior change).

## 6. Recommended Route

Avenue A. Single BED seat. Ship alone; do not bundle with PROP-10.

## 7. Alternatives (parked)

Worker-thread input service; drag algorithm redesign.

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Cancellation mid-drag leaves cursor mid-path | Document/observe; honor ct without throwing away prior moves |
| Scope expands into desktop gate rewrite | Kill — delay-only PROP |

## 9. Open Questions

None blocking.

## 10. Suggested PM Handoff

- `prop_id`: `PROP-6-desktop-drag-async-delay`
- Suggested: `PROP-6.1` BED-01 — replace Sleep with cancellable Delay + tests
- What PM decides first: accept as NOW parallel lane beside PROP-5
