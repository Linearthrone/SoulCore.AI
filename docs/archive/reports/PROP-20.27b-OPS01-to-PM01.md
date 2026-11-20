---
type: report
prop_id: PROP-20.27b
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
updated: 2026-11-18
title: Release Host+Presence refresh for Compass B
machine: home-pc
configuration: Release
depends_on: PROP-20.27
---

# PROP-20.27b — OPS Release refresh

**Result: Pass**

Release ALLSTART with `-RestartHost -ForceRebuild` completed after Compass B (PROP-20.27 retrieve ≤3). Host PID **22752** healthy on `:7710`; Presence Release PID **54856**.

## Acceptance

| Criterion | Result |
| --- | --- |
| `ALLSTART -Configuration Release -RestartHost` (+ ForceRebuild) | **Pass** — ForceRebuild used; solution Release build 0 Error(s); Host restarted |
| Probe `/health` | **Pass** — `status=ok`, `service=SoulCore.Host`, `version=0.1.54`, memory open |
| Presence Release (not Debug) | **Pass** — `House.ChatDesktop.exe` from `bin\Release\net8.0` |
| Compass B code in live Host | **Pass** — `SoulCore.Memory` / `Inference` / `Host` Release DLLs stamped **2026-11-18 03:50** |
| Report with command evidence | **Pass** — this file |

## Commands

```powershell
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# Log: docs/agents/reports/_allstart-prop-20.27b-release.log

Invoke-RestMethod http://127.0.0.1:7710/health
```

## Evidence

### ALLSTART Release rebuild + restart

```text
RestartHost: replacing local Victoria on :7710
Stopping PID 6492 (dotnet) - SoulCore.Host (-RestartHost)
Building SoulCore (Release)...
Stopping House.ChatDesktop PID 42892 (unlock build outputs)
  SoulCore.Memory -> ...\SoulCore.Memory\bin\Release\net8.0\SoulCore.Memory.dll
  SoulCore.Inference -> ...\SoulCore.Inference\bin\Release\net8.0\SoulCore.Inference.dll
  SoulCore.Host -> ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll
  House.ChatDesktop -> ...\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.dll
Build succeeded.
    4 Warning(s)   # CA1416 Windows-only only
    0 Error(s)
Time Elapsed 00:00:04.49
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 22752
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
  model:  deepseek-v4.1-flash:cloud
Starting House.ChatDesktop (Release)...
ok
```

### `/health` probe (post-restart)

```text
status=ok
service=SoulCore.Host
version=0.1.54
bind=127.0.0.1
port=7710
memory.open=True
memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
inference.model=deepseek-v4.1-flash:cloud
charter.anchors=18
```

Full JSON snapshot: `docs/agents/reports/_health-prop-20.27b.json`

### Live processes

| Role | PID | Path / start |
| --- | --- | --- |
| Host (Release) | **22752** | `SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll` — started 2026-11-18 03:50:50 |
| Presence (Release) | **54856** | `House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe` — started 2026-11-18 03:51:04 |

### DLL stamps (Compass B stack)

| Assembly | LastWriteTime |
| --- | --- |
| SoulCore.Memory.dll | 2026-11-18 3:50:47 AM |
| SoulCore.Inference.dll | 2026-11-18 3:50:48 AM |
| SoulCore.Host.dll | 2026-11-18 3:50:49 AM |

## Side seats (not blocking)

- Tailscale serve TCP `:7710` + HTTPS `:8543` enabled
- STT/TTS already healthy
- BrowserCaptureBridge already healthy at `:18710`
- Unreal `connected=false` (optional; Host continues)

## deploy_history / version docs

- No `deploy_history.json` and no `docs/版本变更记录.md` in this tree (same as prior PROP-20.x OPS notes). Not created here — Host/Presence Release restart only; product version remains `0.1.54` from `/health`.

## Notes

- ALLSTART parent shell stays open while Presence (`dotnet run`) is foreground — expected; stack is live without waiting for parent exit 0.
- ForceRebuild used proactively (DLL locks from prior Presence) so Compass B Host/Memory rebuild landed cleanly.
