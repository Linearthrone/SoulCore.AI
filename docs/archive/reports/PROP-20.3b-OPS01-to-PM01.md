---
type: report
prop_id: PROP-20.3b
prop_root: PROP-20-victoria-mind-path
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
title: Memory migration 012 applied via Host rebuild/restart
depends_on: PROP-20.3
---

# PROP-20.3b — OPS apply journal migration 012

**Result: Pass**

Home-pc Host rebuilt (Debug, canonical `start-soulcore.ps1` / Host project) and restarted so Memory opens SQLite and applies `012_thoughts_wants_dreams_journals`. Live DB now has `schema_migrations` row `012`. No destructive rollback. `deploy_history` / version docs not updated (Host rebuild only; no `deploy_history.json` in this tree for Host-only restarts).

## Acceptance

| Criterion | Result |
| --- | --- |
| Host running on build that includes Memory 012 | **Pass** — `SoulCore.Host` healthy on `127.0.0.1:7710`, Memory DLL embeds 012 |
| Evidence `012` in `schema_migrations` | **Pass** — see SQLite query below |
| Report | **Pass** — this file |

## Pre (before apply)

```text
001|initial|2026-07-23T01:16:10.581Z
...
011|victoria_research|2026-10-18T18:21:02.265Z
(no 012)
```

## Commands

```powershell
# Canonical path (succeeded once after Host file locks cleared)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  SoulCore\scripts\start-soulcore.ps1 -Port 7710 -ForceRebuild -RestartHost -SkipPreflight
```

```text
RestartHost: clearing every SoulCore.Host (+ :7710 listener)
Host inventory: 0 process(es), 0 listener(s) on :7710
Building SoulCore...
  SoulCore.Memory -> ...\SoulCore.Memory\bin\Debug\net8.0\SoulCore.Memory.dll
  SoulCore.Host -> ...\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll
Build succeeded.
    6 Warning(s)
    0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 50312
Health: http://127.0.0.1:7710/health
```

Later solution rebuilds raced with `House.ChatDesktop` locking `SoulCore.Protocol.dll`. Final sustained Host start used Host-project-only Debug build (same binaries / Memory 012) then `dotnet SoulCore.Host.dll --urls http://127.0.0.1:7710`:

```text
SoulCore.Host -> ...\bin\Debug\net8.0\SoulCore.Host.dll
Build succeeded.
PID 39628
HEALTH service=SoulCore.Host status=ok version=0.1.54
memory.open=True path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

## Evidence — `schema_migrations` (live DB)

DB: `%LOCALAPPDATA%\SoulCore\memory\soulcore_memory.db`

```text
001|initial|2026-07-23T01:16:10.581Z
002|embedding_vectors|2026-07-24T04:41:46.797Z
003|victoria_tasks|2026-07-27T15:03:21.709Z
004|victoria_workflows|2026-07-27T15:03:21.718Z
005|episodic_source_model|2026-07-27T15:03:21.724Z
006|victoria_journals|2026-07-30T21:50:07.727Z
007|chat_transcript|2026-10-07T12:16:00.151Z
008|victoria_tasks_park|2026-10-13T00:05:35.407Z
009|victoria_sops|2026-10-13T00:05:35.426Z
010|life_hacks_journal|2026-10-13T00:05:35.452Z
011|victoria_research|2026-10-18T18:21:02.265Z
012|thoughts_wants_dreams_journals|2026-11-17T22:51:38.392Z
```

## Evidence — journal books seed (optional smoke)

```text
animation, dreams, environment, feeling, life_hacks, thoughts, wants
```

`thoughts` / `wants` / `dreams` created_at aligned with migration apply: `2026-11-17T22:51:38.389Z`.

## Evidence — Host log

```text
info: SoulCore.Memory.SqliteMemorySession[0]
      SqliteMemorySession ready at C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
info: SoulCore.Host[0]
      SoulCore.Host listening on http://127.0.0.1:7710 ... memory=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

(012 apply log line was from first open after rebuild; subsequent restarts skip already-applied migrations.)

## Notes for PM

- Canonical `start-soulcore.ps1` builds **Debug** (not Release). Release remains blocked by SixLabors license (see PROP-20.1b) — out of scope here; Debug Host is the home-pc script path.
- No rollback of 012 performed.
- `deploy_history` / `docs/版本变更记录.md` not touched (Host-only migration restart).
