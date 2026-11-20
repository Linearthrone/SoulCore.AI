---
type: report
prop_id: PROP-20.3
prop_root: PROP-20-victoria-mind-path
from: DBD-01
to: PM-01
status: Pass
created: 2026-11-17
updated: 2026-11-17
---

# PROP-20.3 — Journal books thoughts / wants / dreams — Pass

## Done

1. Migration `012_thoughts_wants_dreams_journals.sql`: recreate `victoria_journal_books` CHECK to allow `thoughts`, `wants`, `dreams`; seed the three books. Research tables untouched.
2. `SqliteVictoriaJournalRepository` allow-list + sort order extended (feeling → dreams = 1..7).
3. `IVictoriaJournalStore` contract comments updated; research remains a separate store.
4. `SqliteMemorySession.ApplyIfMissing("012", …)` + embedded resource in `SoulCore.Memory.csproj`.
5. `SqliteMemoryStoreJournalTests` updated for seven books, book-scoped writes, research isolation, unknown-book reject.

## Migration

| Item | Value |
| --- | --- |
| Version | `012` |
| Name | `thoughts_wants_dreams_journals` |
| Path | `SoulCore/SoulCore.Memory/Migrations/012_thoughts_wants_dreams_journals.sql` |
| Companion (docs only) | `Schema/012_thoughts_wants_dreams_journals.sql` (not embedded) |
| Pattern | Same as 010: `PRAGMA foreign_keys=OFF` → new table with wider CHECK → copy rows → drop/rename → `INSERT OR IGNORE` seeds → `schema_migrations` |

**Forward apply:** automatic on next `SqliteMemorySession` open when `schema_migrations` lacks version `012`.

**Consumer impact (BED/FED):** new book ids available via existing journal APIs; UI desk is PROP-20.8 (not this ticket). Chat prompt untouched.

## Rollback

SQLite cannot shrink a CHECK in place. Rollback is a reverse recreate (destructive to the three new books if they have entries):

1. Confirm no production rows needed under `thoughts` / `wants` / `dreams` (or export them first).
2. `PRAGMA foreign_keys=OFF`.
3. Recreate `victoria_journal_books` with CHECK limited to `feeling`, `animation`, `environment`, `life_hacks` (copy only those four ids).
4. Delete orphaned `victoria_journal_entries` where `book_id IN ('thoughts','wants','dreams')` **before** restore if FK would block, or delete after drop if FKs are off.
5. Rename table back; `PRAGMA foreign_keys=ON`.
6. `DELETE FROM schema_migrations WHERE version = '012';`.

Prefer forward-fix over rollback. Do not apply destructive prod DDL without PM + OPS.

## Acceptance

| Criterion | Result |
| --- | --- |
| Write to `thoughts` succeeds; lists under that book only | Pass |
| Research paper list does not show private journal row | Pass |
| Tests green with pasted output | Pass |
| Report with migration/rollback notes | Pass |

## Test output

Command:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~SqliteMemoryStoreJournalTests"
```

Result (2026-11-17):

```text
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 1 s - SoulCore.Protocol.Tests.dll (net8.0)
```

Cases:

- `MigrationSeedsSevenJournalBooks_IncludingThoughtsWantsDreams`
- `WriteAndListEntries_ByBook`
- `ThoughtsEntry_DoesNotAppearInResearchPaperList`
- `WriteEntry_UnknownBook_Throws`

## Files

- `SoulCore/SoulCore.Memory/Migrations/012_thoughts_wants_dreams_journals.sql` (new)
- `SoulCore/SoulCore.Memory/Schema/012_thoughts_wants_dreams_journals.sql` (new, docs only)
- `SoulCore/SoulCore.Memory/Repositories/SqliteVictoriaJournalRepository.cs`
- `SoulCore/SoulCore.Memory/IVictoriaJournalStore.cs`
- `SoulCore/SoulCore.Memory/SqliteMemorySession.cs`
- `SoulCore/SoulCore.Memory/SoulCore.Memory.csproj`
- `SoulCore/SoulCore.Protocol.Tests/SqliteMemoryStoreJournalTests.cs`

## Not touched

Research store DDL/API, ChatDesktop UI, Host chat prompt, SMS.
