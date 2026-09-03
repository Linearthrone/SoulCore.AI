---
type: issue
id: ISSUE-20260726-002
severity: P2
status: resolved
created: 2026-07-26
resolved: 2026-07-27
filed_by: BED-01
resolved_by: BED-01
related_task: TASK-131
resolved_in: TASK-20260727-157
gate: none (migration 005 — 003/004 reserved by BED-140/141)
---

# ISSUE-002 — `store_memory` cannot use `source='model'` — schema CHECK + `AllowedSources` reject it

## Status

**Resolved (2026-07-27 / TASK-157).** Migration `005_episodic_source_model.sql` expands the CHECK + `AllowedSources`; `StoreMemoryTool.SourceLabel` is now `"model"`. ISSUE originally recommended migration **003**, but TASK-140 owns `003_victoria_tasks` and TASK-141 owns `004_victoria_workflows`, so this shipped as **005**.

## Severity

**P2 — Spec/schema mismatch. TASK-131 acceptance criterion #3 requires `store_memory` to write rows with `source='model'`, but `'model'` is not an allowed value. Worked around by using `source='chat'` (already means "model-authored" and is distinct from SoulLoop's `'self'`), so QA-130 is not blocked. A dedicated `'model'` label requires a DBD schema migration + `SqliteMemoryStore.AllowedSources` update.**

Not P1 because: the ticket's underlying intent — "distinguish model-authored from SoulLoop-authored" — is already satisfied by the existing `'chat'` vs `'self'` split. The workaround is semantically correct and ships now. The only gap is a cosmetic/provenance-granularity one: a dedicated `'model'` label would let recall filter for `store_memory`-authored rows specifically, separate from the chat-path model-authored rows (`AuthorChatEpisodicAsync`). That is a "nice to have", not a blocker.

## Summary

TASK-131 specified `store_memory` should write episodic rows with `source='model'` to "distinguish model-authored from SoulLoop-authored" (SoulLoop uses `source='self'`). But the schema and store reject that value:

### Evidence (pre-fix)

**`SoulCore/SoulCore.Memory/Schema/001_schema.sql`** — the `episodic_memories.source` CHECK constraint omitted `'model'`.

**`SqliteMemoryStore.AllowedSources`** mirrored the CHECK; `NormalizeSource` coerced unknown values to `'system'`.

## Fix landed (TASK-157)

1. **Migration `005_episodic_source_model.sql`** — table rebuild with expanded CHECK including `'model'` (SQLite cannot ALTER CHECK in place). Preserves child embedding rows (FK off during swap).
2. **`Schema/001_schema.sql`** — canonical CHECK updated for fresh installs.
3. **`SqliteMemoryStore.AllowedSources`** — `"model"` added.
4. **`StoreMemoryTool.SourceLabel`** — `"chat"` → `"model"`.
5. No backfill: pre-fix `store_memory` rows stay `'chat'`.

## Workaround taken in TASK-131 (historical)

`StoreMemoryTool.SourceLabel = "chat"` until migration 005 landed.
