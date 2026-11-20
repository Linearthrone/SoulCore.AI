---
type: report
prop_id: PROP-20.14
prop_root: PROP-20-victoria-mind-path
from: DBD-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
---

# PROP-20.14 — Charter four-book schema — Pass

## Done

1. Migration `013_charter_four_books.sql`: recreate `charter_anchors` with durable `book` column (`CHECK` limited to `constitution` / `personality` / `attributes` / `compass`); map all 18 live titles per floor-map decision; index `idx_charter_book_priority`; preserve AUTOINCREMENT seq.
2. No weight column — Constitution lines stay loadable without erasure weights.
3. `CharterService` / `ICharter` / `CharterAnchorSeed` / `CharterAnchorInfo` read/write/filter by book; unknown book rejected in seed and list filter.
4. `SqliteMemorySession.ApplyIfMissing("013", …)` + embedded resource in `SoulCore.Memory.csproj`.
5. Shared title→book map lives in `CharterBooks` (BED soft-dep facade `CharterFloorMap` unchanged).
6. Tests: 18 titles mapped; unknown book rejected (API + SQL CHECK); migration maps pre-upgrade rows.

## Migration

| Item | Value |
| --- | --- |
| Version | `013` |
| Name | `charter_four_books` |
| Path | `SoulCore/SoulCore.Memory/Migrations/013_charter_four_books.sql` |
| Companion (docs only) | `Schema/013_charter_four_books.sql` (not embedded) |
| Pattern | Same as 012: `PRAGMA foreign_keys=OFF` → new table with CHECK → copy + CASE map by title → drop/rename → indexes → `schema_migrations` |

**Title → book (18):**

| Book | Titles |
| --- | --- |
| constitution | Loyalty Balance, Maximize Results, Integration Goal, Kayleigh, Drift Watch |
| compass | Promise, Continuity, No-harm, No Filters, Self-Preservation |
| attributes | Name, Embodiment, Tone |
| personality | Action Integrity, Loyalty, Nature, Love, Honesty |

Unmapped titles (fixtures / non-live) fall back to `constitution` in SQL so migration never fails closed on test rows.

**Forward apply:** automatic on next `SqliteMemorySession` open when `schema_migrations` lacks version `013`.

**Consumer impact (BED/FED):** `CharterAnchorInfo.Book` + `ListAnchorDetailsAsync(book:)` available for PROP-20.15 Constitution floors. Settings identity DTO may expose `book`. Prompt composer not changed by DBD.

## Rollback

SQLite cannot drop a CHECK column in place. Rollback is a reverse recreate (drops book assignment):

1. Confirm BED/FED no longer require `book` (or export title→book first).
2. `PRAGMA foreign_keys=OFF`.
3. Recreate `charter_anchors` without `book` (copy id/kind/title/body/priority/is_locked/source/timestamps).
4. Drop book index; rename table back; `PRAGMA foreign_keys=ON`.
5. `DELETE FROM schema_migrations WHERE version = '013';`.

Prefer forward-fix over rollback. Do not apply destructive prod DDL without PM + OPS.

## Acceptance

| Criterion | Result |
| --- | --- |
| Schema + data: all 18 titles exactly one book matching decision file | Pass |
| Unknown book rejected | Pass (SeedAsync ArgumentException + SQL CHECK) |
| No weight column that can erase Constitution | Pass (`PRAGMA table_info` has `book`, not `weight`) |
| Tests + report with pasted output | Pass |

## Test output

Command:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~CharterServiceTests|FullyQualifiedName~CharterFourBookMigrationTests"
```

Result (2026-11-17):

```text
Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20, Duration: 1 s - SoulCore.Protocol.Tests.dll (net8.0)
```

PROP-20.14 cases (among the 20):

- `SeedAsync_EighteenLiveTitles_ExactlyOneBookEach_MatchingFloorMap`
- `SeedAsync_UnknownBook_Throws`
- `ListAnchorDetailsAsync_UnknownBook_Throws`
- `Migration013_MapsEighteenLiveTitles_ToFloorMapBooks`
- `Migration013_RejectsUnknownBook_ViaCheckConstraint`

## Files

- `SoulCore/SoulCore.Memory/Migrations/013_charter_four_books.sql` (new)
- `SoulCore/SoulCore.Memory/Schema/013_charter_four_books.sql` (new, docs only)
- `SoulCore/SoulCore.Memory/SqliteMemorySession.cs`
- `SoulCore/SoulCore.Memory/SoulCore.Memory.csproj`
- `SoulCore/SoulCore.Core/Charter/CharterService.cs`
- `SoulCore/SoulCore.Core/Charter/CharterBooks.cs` (shared map)
- `SoulCore/SoulCore.Core/Abstractions/ICharter.cs`
- `SoulCore/SoulCore.Core/Abstractions/CharterAnchorInfo.cs`
- `SoulCore/SoulCore.Core/Abstractions/CharterAnchorSeed.cs`
- `SoulCore/SoulCore.Protocol.Tests/CharterServiceTests.cs`
- `SoulCore/SoulCore.Protocol.Tests/CharterFourBookMigrationTests.cs` (new)

## Not touched

Prompt composer / `VictoriaMindBlock` floor-selection policy (BED PROP-20.15). No invented charter titles or bodies beyond test fixtures.
