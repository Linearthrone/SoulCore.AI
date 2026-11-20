---
type: report
prop_id: PROP-18.6
prop_root: PROP-18-chronology-and-felt-persistence
from: DBD-01
to: PM-01
status: Pass
created: 2026-11-19
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
---

# PROP-18.6 — Activities log schema — Pass

## Done

1. Migration **015** creates `victoria_activities_log` with Q1 columns: `entry_no`, `occurred_at`, `activity`, `subject`, `session_desc`, optional `journal_book_id` / `journal_entry_id`, `created_at`.
2. Indexes: `idx_activities_occurred` (recent-first) + `idx_activities_subject_occurred` (subject filter, recent-first).
3. Research papers stay in `victoria_research_*` (011) — no FK / no merge into activities log. Journal links are optional soft refs (`ON DELETE SET NULL`) only.
4. Thin store for BED-18.7 / FED-18.3: `IVictoriaActivitiesLogStore` + `SqliteVictoriaActivitiesLogRepository`, DI-registered in Host.
5. Rollback script + tests covering migrate, list/filter, CHECK, and rollback preserving research/journals.

## Schema

| Column | Role |
| --- | --- |
| `entry_no` | Entry No. (AUTOINCREMENT PK) |
| `occurred_at` | Date/Time |
| `activity` | Activity (e.g. leisure / research / parked-work) |
| `subject` | Subject / Topic / Focus |
| `session_desc` | Desc — session-specific blurb (nonempty CHECK) |
| `journal_book_id` / `journal_entry_id` | Optional private-journal append hooks |
| `created_at` | Insert audit |

## Rollback notes

- Script: `SoulCore/SoulCore.Memory/Migrations/015_victoria_activities_log_rollback.sql`
- Drops indexes + `victoria_activities_log`, deletes `schema_migrations` row `015`.
- Does **not** touch journals or research tables.
- Dev/empty DBs only after backup; never prod without PM+OPS.

## Consumer impact

| Role | Note |
| --- | --- |
| **BED-18.7** | Inject `IVictoriaActivitiesLogStore`; `AppendAsync` + `ListRecentAsync`; Desc must stay non-generic |
| **FED-18.3** | List APIs ready for desk columns; map `session_desc` → Desc |
| **Research** | Papers remain `victoria_research_papers` — activities may name a subject in free text only |

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~Prop186ActivitiesLogMigrationTests"

Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

Cases: table+indexes+ledger; append/list recent-first + subject filter + journal link; empty-desc CHECK; rollback drops log / keeps research+journals.

## Files

- `SoulCore/SoulCore.Memory/Migrations/015_victoria_activities_log.sql`
- `SoulCore/SoulCore.Memory/Migrations/015_victoria_activities_log_rollback.sql`
- `SoulCore/SoulCore.Memory/Schema/015_victoria_activities_log.sql`
- `SoulCore/SoulCore.Memory/SqliteMemorySession.cs` (wire 015)
- `SoulCore/SoulCore.Memory/SoulCore.Memory.csproj` (embedded resource)
- `SoulCore/SoulCore.Memory/IVictoriaActivitiesLogStore.cs`
- `SoulCore/SoulCore.Memory/Repositories/SqliteVictoriaActivitiesLogRepository.cs`
- `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/MemoryServiceCollectionExtensions.cs`
- `SoulCore/SoulCore.Protocol.Tests/Prop186ActivitiesLogMigrationTests.cs`

## Verdict

**Pass** — schema, indexes, research boundary, rollback notes, and migrate tests landed. BED-18.7 unblocked for writers.
