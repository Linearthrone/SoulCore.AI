---
type: report
prop_id: PROP-20.44b
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-18
seat: OPS-HOME
upstream_task: docs/agents/tasks/PROP-20.44b-PM01-to-OPS01.md
depends_on: PROP-20.44
---

# PROP-20.44b — OPS report (ship 1.0.0)

## Verdict: **Pass**

Release ALLSTART with `-RestartHost -ForceRebuild` shipped product **1.0.0**. Live `GET /health` reports `version=1.0.0`. Presence (House.ChatDesktop) is up from Release binaries and left running (not stopped).

## Commands / evidence

```text
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# tee: docs/agents/reports/_allstart-prop-20.44b-release.log
```

Host start excerpt:

```text
RestartHost: replacing local Victoria on :7710
RestartHost: clearing every SoulCore.Host (+ :7710 listener)
Stopping PID 4896 (dotnet) - SoulCore.Host (-RestartHost)
Stopping House.ChatDesktop PID 54340 (unlock build outputs)
Building SoulCore (Release)...
Build succeeded.  4 Warning(s)  0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 18704
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
  model:  deepseek-v4.1-flash:cloud
=== ALLSTART: House.ChatDesktop (Victoria Presence) ===
GUI target: 127.0.0.1:7710
Starting House.ChatDesktop (Release)...
```

(ALLSTART parent remains attached to ChatDesktop via `dotnet run` — expected desk behavior; Presence process is alive.)

### Health

```text
GET http://127.0.0.1:7710/health
  status=ok
  service=SoulCore.Host
  version=1.0.0
  port=7710
  memory.open=True
  memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
  inference.provider=ollama-cloud
  model=deepseek-v4.1-flash:cloud
```

Raw capture: `docs/agents/reports/_prop-20.44b-health.json`

### DLL / process freshness (1.0.0)

```text
Directory.Build.props Version=1.0.0 InformationalVersion=1.0.0
HostDLL LastWrite=2026-11-18T21:46:09-05:00 FileVersion=1.0.0.0 ProductVersion=1.0.0+4926585ea39ba15818fd4841532e7e1fe59ffd0f
GuiDLL  LastWrite=2026-11-18T21:46:09-05:00 FileVersion=1.0.0.0 ProductVersion=1.0.0+4926585ea39ba15818fd4841532e7e1fe59ffd0f
HostPID=18704 Alive=True StartTime=2026-11-18 21:46:11
ChatDesktop PID=46712 StartTime=2026-11-18 21:46:24
ChatDesktop Path=...\House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe
```

## Checklist

| Check | Result |
| --- | --- |
| `ALLSTART -Configuration Release -RestartHost -ForceRebuild` | Pass |
| `/health` version **1.0.0** | Pass |
| Presence Release up (ChatDesktop not left down) | Pass |
| Host memory open on local Victoria path | Pass |

## Follow-up (ALLSTART shell abort)

The ALLSTART parent was aborted while attached to `dotnet run` ChatDesktop, which took Presence down. Host stayed healthy (`1.0.0`, PID 18704). Presence restored detached:

```text
Start-Process House.ChatDesktop.exe (Release) → PID 43552
GET /health → version=1.0.0 status=ok
Presence UP Path=...\bin\Release\net8.0\House.ChatDesktop.exe
```

## deploy_history / version docs

- No `deploy_history.json` and no `docs/版本变更记录.md` in this tree (same as prior PROP-20.x OPS notes). Not created here — product version SoT is `Directory.Build.props` + live `/health` (`1.0.0`).
