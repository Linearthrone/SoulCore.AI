---
type: report
prop_id: PROP-5.1-5.3
from: BED-01
to: PM-01
status: Completed
created: 2026-09-05
branch: cursor/prop5-sqlite-gate-8a1f
base: cursor/tina-wave-prop5-11-8a1f
verdict: Pass
---

# PROP-5.1 + 5.2 + 5.3 — BED-01 completion report

## Verdict: **Pass**

All three Host-safe slices landed on `cursor/prop5-sqlite-gate-8a1f` (child of `cursor/tina-wave-prop5-11-8a1f`). Focused Memory + Protocol tests green (29/29).

## Ownership model (before → after)

**Before:** `SqliteMemoryStore` held one long-lived `SqliteConnection` with `_gate` used only on dispose; all async command paths raced the connection. `CharterService` xmldoc claimed an independent DB with no Host DI, but `Program.cs` constructed `new CharterService(memoryOptions.ResolveDbPath())` — a second ungated long-lived opener on the same file. SoulLoop hosted timer and WS `loop.tick` could overlap `TickAsync`; `_tickCount++` was unsynchronized.

**After:** One path-keyed process-wide gate (`SqlitePathGate`) serializes all DB command paths for both `SqliteMemoryStore` and `CharterService` on the same normalized absolute path. Each type keeps its own connection (Avenue C light) but **never** issues concurrent commands without the shared gate. Memory migrations remain DDL authority; charter `EnsureSchema` is idempotent fallback for test DBs. SoulLoop uses a separate instance-level `SemaphoreSlim(1,1)` single-flight gate (skip on overlap) plus `Interlocked.Increment` for tick counter. `busy_timeout` = 5000 ms on open (connection string + PRAGMA).

## How the gate works

1. **`SqlitePathGate.ForPath(dbPath)`** — returns a shared `SemaphoreSlim(1,1)` keyed by normalized absolute path (`SoulCore.Core/Sqlite/SqlitePathGate.cs`).
2. **`SqliteMemoryStore`** — every public async DB method runs inside `RunDbAsync` → `SqliteDbGate.RunAsync(_dbGate, …)`; dispose waits on the gate before closing the connection.
3. **`CharterService`** — same `_dbGate` instance for `ResolveDbPath()`; constructor open/schema and all reads/writes acquire the gate for short critical sections only.
4. **`SoulLoopScaffold`** — independent `_tickFlight` gate: overlapping `TickAsync` callers get `WaitAsync(0)` failure → debug log → immediate return (no queue).

Gate is **not** held across embedding/network/LLM I/O — store methods remain DB-only; callers compute embeddings before calling `StoreEmbeddingAsync`.

## Files changed

| File | PROP | Change |
| --- | --- | --- |
| `SoulCore/SoulCore.Core/Sqlite/SqlitePathGate.cs` | 5.2/5.3 | New path-keyed gate + busy_timeout constant |
| `SoulCore/SoulCore.Memory/SqliteDbGate.cs` | 5.2 | Internal RunAsync helpers |
| `SoulCore/SoulCore.Memory/SqliteMemoryStore.cs` | 5.2 | Gate all command paths; busy_timeout on open |
| `SoulCore/SoulCore.Host/Loop/SoulLoopScaffold.cs` | 5.1 | Single-flight TickAsync; Interlocked tick counter |
| `SoulCore/SoulCore.Core/Charter/CharterService.cs` | 5.3 | Shared gate; xmldoc honesty; busy_timeout |
| `SoulCore/SoulCore.Host/Program.cs` | 5.3 | DI comment documents shared-path ownership |
| `SoulCore/SoulCore.Protocol.Tests/SoulLoopScaffoldSingleFlightTests.cs` | 5.1 | Overlap + parallel tick proofs |
| `SoulCore/SoulCore.Protocol.Tests/SqlitePathGateConcurrencyTests.cs` | 5.2/5.3 | Memory + charter concurrent ops on same path |

## Test output

```
dotnet test SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
  --filter "FullyQualifiedName~CharterService|FullyQualifiedName~SqliteMemory|FullyQualifiedName~SoulLoopScaffold|FullyQualifiedName~SqlitePathGate"

Passed!  - Failed: 0, Passed: 29, Skipped: 0, Total: 29
```

## Fences respected

- No `ChatWebSocketHandler` structural edits
- No Hermes purge, DI module extract, EF, vector index, or `NativeDesktopControlBackend` changes

## Commits (branch `cursor/prop5-sqlite-gate-8a1f`)

1. `feat(prop-5.1): SoulLoop TickAsync single-flight and atomic tick counter`
2. `feat(prop-5.2): serialize SqliteMemoryStore DB commands with path gate`
3. `test(prop-5): add SoulLoop single-flight and sqlite path gate tests` *(includes 5.3 charter + Program.cs in same commit due to parallel git lock)*

## Next

- **PROP-5.4 (QA-01):** concurrent soak — chat write + SMS/memory write + dual tick + charter read under load.
