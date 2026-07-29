---
type: issue
id: ISSUE-20260726-002
severity: P2
status: schema-landed-tool-pending
updated: 2026-07-29
created: 2026-07-26
filed_by: BED-01
related_task: TASK-131
follow_up: TASK-158
gate: none (DBD-157 Pass PR #4; BED-158 flips StoreMemoryTool)
---

# ISSUE-002 — `store_memory` cannot use `source='model'` — schema CHECK + `AllowedSources` reject it

## Severity

**P2 — Spec/schema mismatch. TASK-131 acceptance criterion #3 requires `store_memory` to write rows with `source='model'`, but `'model'` is not an allowed value. Worked around by using `source='chat'` (already means "model-authored" and is distinct from SoulLoop's `'self'`), so QA-130 is not blocked. A dedicated `'model'` label requires a DBD schema migration + `SqliteMemoryStore.AllowedSources` update.**

Not P1 because: the ticket's underlying intent — "distinguish model-authored from SoulLoop-authored" — is already satisfied by the existing `'chat'` vs `'self'` split. The workaround is semantically correct and ships now. The only gap is a cosmetic/provenance-granularity one: a dedicated `'model'` label would let recall filter for `store_memory`-authored rows specifically, separate from the chat-path model-authored rows (`AuthorChatEpisodicAsync`). That is a "nice to have", not a blocker.

## Summary

TASK-131 specified `store_memory` should write episodic rows with `source='model'` to "distinguish model-authored from SoulLoop-authored" (SoulLoop uses `source='self'`). But the schema and store reject that value:

### Evidence

**`SoulCore/SoulCore.Memory/Schema/001_schema.sql` lines 28–31** — the `episodic_memories.source` CHECK constraint:

```sql
source          TEXT        NOT NULL
                CHECK (source IN (
                    'self', 'chat', 'imported', 'observation', 'correction', 'system'
                )),
```

`'model'` is **not** in the allowed set.

**`SoulCore/SoulCore.Memory/SqliteMemoryStore.cs` lines 19–22 + 428–435** — `AllowedSources` mirrors the CHECK, and `NormalizeSource` coerces unknown values to `'system'`:

```csharp
private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
{
    "self", "chat", "imported", "observation", "correction", "system"
};

private static string NormalizeSource(string? sourceLabel)
{
    if (string.IsNullOrWhiteSpace(sourceLabel))
        return "system";
    var trimmed = sourceLabel.Trim().ToLowerInvariant();
    return AllowedSources.Contains(trimmed) ? trimmed : "system";
}
```

So writing `source='model'` literally would either:
- be **rejected by SQLite** with a CHECK constraint violation (if bypassing `NormalizeSource`), or
- be **silently coerced to `'system'`** by `NormalizeSource` (the path `WriteEpisodicAsync` actually takes).

Neither yields a row with `source='model'`.

## Impact on TASK-131

- **Acceptance criterion #3** ("`store_memory` writes a row with `source='model'` (not `'self'`)") cannot be satisfied as written.
- The ticket's **intent** ("distinguish model-authored from SoulLoop-authored") **is already satisfied** by the existing convention:
  - SoulLoop writes `source='self'` (`SoulLoopScaffold.cs` line 164).
  - The chat path's model-authored episodics write `source='chat'` (`ChatWebSocketHandler.cs` line 447 — `AuthorChatEpisodicAsync`, BED-108).
  - `store_memory` reuses `'chat'` so it lands in the same provenance bucket as other model-authored memories and remains distinct from SoulLoop's `'self'`.

## Workaround taken in TASK-131

`StoreMemoryTool.SourceLabel = "chat"` (constant). The tool writes with `source='chat'`, which:
- Is schema-valid (passes the CHECK).
- Is the existing label for model-authored episodic memories.
- Is distinct from `source='self'` (SoulLoop-authored).
- Distinguishes model-authored from loop-authored, satisfying the ticket's intent.

The tool's `Data` payload also returns `source = "chat"` so the model/host can see the provenance.

## Recommended fix (DBD follow-up)

If a dedicated `'model'` provenance label is still desired (to distinguish `store_memory`-authored rows from chat-path `AuthorChatEpisodicAsync` rows), this requires:

1. **DBD schema migration `003`** — `ALTER TABLE episodic_memories` cannot drop a CHECK constraint in SQLite; the migration must rebuild the table with the expanded CHECK:
   ```sql
   CHECK (source IN (
       'self', 'chat', 'imported', 'observation', 'correction', 'system', 'model'
   ))
   ```
2. **`SqliteMemoryStore.AllowedSources`** — add `"model"` to the `HashSet`.
3. **`StoreMemoryTool.SourceLabel`** — change from `"chat"` to `"model"`.
4. Backfill consideration: existing `store_memory` rows written as `'chat'` stay `'chat'` (no rewrite needed; the label change only affects new rows).

This is a DBD-owned change (schema + store). Coordinated with PM as a follow-up ticket; **not** in BED-131's lane (constraints: "Do not change `IMemoryStore` interface").

## Reproduction

```csharp
// In a real SqliteMemoryStore (not the fake):
await store.WriteEpisodicAsync("test", "model", ct);
// → SQLiteException: CHECK constraint failed: episodic_memories
// (or silently stored as source='system' via NormalizeSource, depending on path)
```

## Status

**Open.** Workaround landed in TASK-131 (uses `'chat'`). Recommend DBD pick up the migration as a follow-up; lowering to P2 since QA-130 is unblocked.
