---
type: report
prop_id: PROP-20.27c
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
updated: 2026-11-18
title: Restore Presence Release after aborted ALLSTART overlap
machine: home-pc
configuration: Release
depends_on: PROP-20.27b
---

# PROP-20.27c — OPS Presence restore

**Result: Pass**

Host stayed up (no ForceRebuild / no RestartHost). Presence Release restored against `127.0.0.1:7710` after aborted overlapping ALLSTART left ChatDesktop down.

## Acceptance

| Criterion | Result |
| --- | --- |
| Do **not** ForceRebuild Host unless health fails | **Pass** — Host health `ok` before start; no `-ForceRebuild`, no `-RestartHost` |
| Start Presence Release vs `127.0.0.1:7710` | **Pass** — `start-desktopgui.ps1 -Configuration Release` (ALLSTART Presence path) |
| Confirm Presence process | **Pass** — `House.ChatDesktop.exe` PID **50332** from `bin\Release\net8.0` |
| Confirm Host still ok | **Pass** — same Host PID **22752**, `/health` `status=ok`, `version=0.1.54`, port **7710** |
| Report with evidence | **Pass** — this file |

## Commands

```powershell
# Preflight (Host already healthy; ChatDesktop absent)
Invoke-RestMethod http://127.0.0.1:7710/health
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'SoulCore\.Host|House\.ChatDesktop' }

# Presence-only (no Host rebuild)
.\start-desktopgui.ps1 -Configuration Release
# launched detached: powershell -File start-desktopgui.ps1 -Configuration Release
# launcher_pid=45468

# Postflight
Invoke-RestMethod http://127.0.0.1:7710/health
Get-Process House.ChatDesktop
```

## Evidence

### Preflight Host (unchanged from PROP-20.27b)

```text
status=ok service=SoulCore.Host version=0.1.54 port=7710
host_pid=22752
host_cmd="C:\Program Files\dotnet\dotnet.exe" ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710
(no House.ChatDesktop process)
```

`/health` (compressed JSON excerpt):

```json
{"status":"ok","service":"SoulCore.Host","version":"0.1.54","bind":"127.0.0.1","port":7710,"phase":1,"memory":{"open":true,"path":"C:\\Users\\kurtw\\AppData\\Local\\SoulCore\\memory\\soulcore_memory.db"}}
```

### Presence start (Release)

```text
launcher_pid=45468
GUI target: 127.0.0.1:7710
Starting House.ChatDesktop (Release)...

Id          : 50332
ProcessName : House.ChatDesktop
Path        : C:\Users\kurtw\.house_victoria\SoulCore.AI\House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe
StartTime   : 11/18/2026 4:00:09 AM

dotnet run --project ...\House.ChatDesktop.csproj -c Release  (PID 50308)
```

### Postflight Host + Presence

```text
status=ok service=SoulCore.Host version=0.1.54 port=7710
host_pid=22752
presence_pid=50332 start=11/18/2026 04:00:09
path=...\House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe
exe LastWriteTime=11/17/2026 11:27:39 PM (existing Release build; no ForceRebuild)
```

## Notes

- No `deploy_history.json` / `docs/版本变更记录.md` update — Presence-only restore; product version remains `0.1.54` from `/health`.
- Parent `dotnet run` launcher (PID 50308) + GUI (PID 50332) stay live; Host untouched.
