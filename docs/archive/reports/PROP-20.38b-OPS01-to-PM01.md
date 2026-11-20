---
type: report
prop_id: PROP-20.38b
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-18
seat: OPS-HOME
depends_on: PROP-20.38
related: PROP-20.37b
---

# PROP-20.38b — OPS report (Presence Release for FED-20.38)

## Verdict: **Pass**

Serialized after **PROP-20.37b**: Host Identity already exposed `bondCounters`/`soak` (migration 014). Stopped locked ChatDesktop, rebuilt Presence **Release** into normal `bin\Release\net8.0`, started against `:7710` with repo `.env` `SOULCORE_*` loaded.

## Preconditions (37b)

```text
GET http://127.0.0.1:7710/health -> ok version=0.1.54 port=7710
GET /settings/identity -> bondCounters=True soak=True phase=display_only
schema_migrations 014 growth_bond @ 2026-11-19T02:10:47.089Z
```

## Presence rebuild + start

```text
Stop-Process House.ChatDesktop (was PID 54596 locking bin\Release)
dotnet build House\House.ChatDesktop\House.ChatDesktop.csproj -c Release
BUILD_EXIT=0  (0 Warning(s), 0 Error(s))
EXE: House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe
mtime: 2026-11-18T21:18:50.4598921-05:00  size: 438784

loaded 30 SOULCORE_* keys from repo .env (values not logged)
HOUSE_SOULCORE_HOST=127.0.0.1 HOUSE_SOULCORE_PORT=7710
Start-Process House.ChatDesktop.exe
PRESENCE_OK PID=53828 Path=...\bin\Release\net8.0\House.ChatDesktop.exe
```

Build log: `docs/agents/reports/_build-prop-20.38b-chatdesktop.log`

## Post-check

```text
Host still ok on :7710; identity bondCounters/soak present
ChatDesktop running from normal Release bin (not temp out)
```

## Notes

- Combined with 37b Release ALLSTART (`_allstart-prop-20.37b-release.log`): Host side already done; this ticket only needed Presence unlock/rebuild/start after Identity growth fields were live.
- Prior FED temp-out build was required while PID 54596 locked the normal Release apphost; lock cleared before this rebuild.
