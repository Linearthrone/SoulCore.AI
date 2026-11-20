---
type: report
prop_id: PROP-20.14b
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
title: Apply Memory migration 013 (charter four books) via Debug Host rebuild/restart
machine: home-pc
configuration: Debug
not: Release production
---

# PROP-20.14b — OPS migration 013

**Result: Pass** (Debug Host only — **not** Release)

Live Memory DB now has `schema_migrations` row `013 / charter_four_books`. `charter_anchors.book` is present and populated (18 anchors across four books).

## Acceptance

| Criterion | Result |
| --- | --- |
| Debug Host rebuild + restart | **Pass** — Host-project Debug rebuild; Host DLL started on `:7710` |
| `013` in `schema_migrations` | **Pass** — see evidence below |
| Optional: anchors include `book` | **Pass** — column + `/settings/identity` sample |
| Report with command evidence | **Pass** — this file |
| Release production claim | **N/A / refused** — Debug only |

## Commands

```powershell
# Pre-state: schema stopped at 012; charter_anchors had no book column.
# ALLSTART -ForceRebuild failed: House.ChatDesktop (PID 24724) locked Protocol.dll (MSB3027).

dotnet build SoulCore\SoulCore.Host\SoulCore.Host.csproj -c Debug --no-incremental
# Build succeeded (ImageSharp Debug license warnings only; 0 Error(s)).
# Embedded resource confirmed: SoulCore.Memory.Migrations.013_charter_four_books.sql

Stop-Process -Name House.ChatDesktop -Force   # clear file lock
# Start rebuilt Host (same sustained path as PROP-20.3b when solution rebuild raced Presence):
dotnet SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710

# Presence relaunched after Host healthy:
dotnet run --project House\House.ChatDesktop\House.ChatDesktop.csproj -c Debug --no-build
```

## Evidence

### Host health (`GET http://127.0.0.1:7710/health`)

```text
status=ok
service=SoulCore.Host
port=7710
memory.open=True
memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

Host process: `dotnet … SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710` (PID **36752**).

Log:

```text
SqliteMemorySession ready at C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
SoulCore.Host listening on http://127.0.0.1:7710
```

### Memory migration 013

DB: `C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db`

`SELECT version, name FROM schema_migrations ORDER BY CAST(version AS INTEGER);`

```text
001	initial
002	embedding_vectors
003	victoria_tasks
004	victoria_workflows
005	episodic_source_model
006	victoria_journals
007	chat_transcript
008	victoria_tasks_park
009	victoria_sops
010	life_hacks_journal
011	victoria_research
012	thoughts_wants_dreams_journals
013	charter_four_books
```

### Optional — `book` on anchors

`PRAGMA table_info(charter_anchors)` includes `book`.

`SELECT book, COUNT(*) FROM charter_anchors GROUP BY book ORDER BY book;`

```text
attributes	3
compass	5
constitution	5
personality	5
```

`GET /settings/identity` → HTTP 200; `anchors` len=18; sample keys include `book` (e.g. Name→attributes, Embodiment→attributes, Nature→personality). Group counts match SQLite.

### Presence

`House.ChatDesktop` Debug relaunched after Host up (PID **42080**); `HOUSE_SOULCORE_PORT=7710`.

## Notes

- First `.\ALLSTART.ps1 -Configuration Debug -RestartHost -ForceRebuild …` aborted on ChatDesktop DLL lock; Host-project build + direct Host DLL start applied migration (Host open runs `SqliteMemorySession.ApplyMigrations`).
- Release ALLSTART still out of scope (SixLabors / PROP-20.1d).
- No `deploy_history.json` / version-doc update (Host rebuild/restart only; no product version bump).
