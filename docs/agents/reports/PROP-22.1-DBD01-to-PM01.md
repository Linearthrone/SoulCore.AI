# PROP-22.1 — DBD-01 → PM-01: operator work items + agenda (migration 016)

**Status:** done (backend data layer). Contract: `docs/agents/unexecuted_proposals/presence-glass-shell.md` §3.1 / §3.2.

## Files touched

| File | Change |
|---|---|
| `SoulCore/SoulCore.Memory/Migrations/016_operator_work_and_agenda.sql` | new — `operator_work_items`, `operator_agenda_items`, indexes, `schema_migrations` row `016` |
| `SoulCore/SoulCore.Memory/SoulCore.Memory.csproj` | `EmbeddedResource` LogicalName `SoulCore.Memory.Migrations.016_operator_work_and_agenda.sql` |
| `SoulCore/SoulCore.Memory/SqliteMemorySession.cs` | `ApplyIfMissing("016", …)` after 015 |
| `SoulCore/SoulCore.Memory/IOperatorWorkItemStore.cs` | new — interface, `OperatorWorkItem` record, `OperatorWorkItemRules` |
| `SoulCore/SoulCore.Memory/IOperatorAgendaStore.cs` | new — interface, `OperatorAgendaItem` record |
| `SoulCore/SoulCore.Memory/Repositories/SqliteOperatorWorkItemRepository.cs` | new — List / Create / Get / Delete via `RunDbAsync` + parameters |
| `SoulCore/SoulCore.Memory/Repositories/SqliteOperatorAgendaRepository.cs` | new — List / Create / Get / Delete; `HH:mm` + weekday 0–6 validation |
| `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/MemoryServiceCollectionExtensions.cs` | registered both repos + interfaces as singletons |

## Schema notes

- `operator_work_items`: all contract columns; CHECKs on `kind` (task|project), `priority` 1–5, 0/1 flags, `status` (open|done|cancelled), non-blank title. `phase_checkpoints_json` / `deliverables_json` are only persisted for `kind=project` (NULL for tasks).
- `operator_agenda_items`: CHECK `weekday` 0–6 (0=Mon); `time_local` `HH:mm` validated in the repository.
- `victoria_tasks` untouched.

## Evidence

```
> dotnet build SoulCore/SoulCore.Memory/SoulCore.Memory.csproj
Build succeeded.    0 Warning(s)    0 Error(s)
```

Fresh DB, Host start log (isolated copy on port 7799, temp DB):

```
Applied Memory migration 015 to ...\prop22-smoke.db
Applied Memory migration 016 to ...\prop22-smoke.db
SqliteMemorySession ready at ...\prop22-smoke.db
```

Round-trip through the repositories (via the PROP-22.2 routes) — project row persisted with JSON columns, task row has NULL project-only columns, deletes return true/false:

```
POST /operator/agenda {"title":"Standup","weekday":0,"time_local":"09:30","notes":"daily"} -> 200 id=1
DELETE /operator/agenda/1 -> 200 {"deleted":1}
```

## Not done / notes

- No update/status-change method (contract asks List/Create/Delete only).
- No dedicated unit tests added for the repositories (verified end-to-end against a real SQLite file instead).
