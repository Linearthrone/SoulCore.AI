---
type: report
prop_id: PROP-18.6b
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-19
seat: OPS-HOME
depends_on: PROP-18.6
---

# PROP-18.6b — OPS report (Release Host + migration 015)

## Verdict: **Pass**

Release ALLSTART with `-RestartHost -ForceRebuild` rebuilt/restarted Host on `:7710`. Migration **015** (`victoria_activities_log`) applied; `GET /health` is ok; live DB has `schema_migrations` row `015`.

## Commands / evidence

```text
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild -SkipVoice -SkipPlaywrightInstall
# tee: docs/agents/reports/_allstart-prop-18.6b-release.log
```

Host start excerpt (from ALLSTART log):

```text
RestartHost: replacing local Victoria on :7710
RestartHost: clearing every SoulCore.Host (+ :7710 listener)
Stopping PID 18704 (dotnet) - SoulCore.Host (-RestartHost)
Building SoulCore (Release)...
Build succeeded.  1 Warning(s)  0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 58832
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

Host log (SqliteMemorySession):

```text
Applied Memory migration 015 to C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
SqliteMemorySession ready at C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

### Migration 015

```text
python sqlite3 probe on soulcore_memory.db:
('015', 'victoria_activities_log', '2026-11-19T10:08:57.723Z')
HAS_015= True
activities_tables= ['victoria_activities_log']
victoria_activities_log_cols= [
  ('entry_no', 'INTEGER'), ('occurred_at', 'TEXT'), ('activity', 'TEXT'),
  ('subject', 'TEXT'), ('session_desc', 'TEXT'),
  ('journal_book_id', 'TEXT'), ('journal_entry_id', 'INTEGER'), ('created_at', 'TEXT')
]
```

### Health

```text
GET http://127.0.0.1:7710/health -> status=ok version=1.0.0 port=7710
service=SoulCore.Host memory.open=true
memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

## Checklist

| Check | Result |
| --- | --- |
| `ALLSTART -Configuration Release -RestartHost -ForceRebuild` Host healthy | Pass |
| Host log Applied Memory migration 015 | Pass |
| `schema_migrations` contains `015` / `victoria_activities_log` | Pass |
| `victoria_activities_log` table present with Q1 columns | Pass |
| `GET /health` status=ok | Pass |

## Notes

- Voice skipped (`-SkipVoice`); Playwright verify skipped (`-SkipPlaywrightInstall`).
- Presence: prior `House.ChatDesktop` Release process already running (PID 30612); ALLSTART `dotnet run` for ChatDesktop may still be waiting on that path — Host/migration acceptance for **18.6b** is green regardless.
- `deploy_history.json` / version changelog not updated (Host restart + schema apply only; no product version bump in this prop).
