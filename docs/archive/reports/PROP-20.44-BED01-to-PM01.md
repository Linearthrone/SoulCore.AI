---
type: report
prop_id: PROP-20.44
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-18
decision: docs/agents/unexecuted_proposals/prop-20-v1-release-decision.md
---

# PROP-20.44 — Version 1.0.0 — Pass

## Result

**Pass.** `Version` and `InformationalVersion` set to **1.0.0** in all three required files; aligned; no feature edits.

## Changes (version only)

| File | Was | Now |
| --- | --- | --- |
| `Directory.Build.props` | 0.1.53 | **1.0.0** |
| `SoulCore/SoulCore.Host/SoulCore.Host.csproj` | 0.1.54 | **1.0.0** |
| `House/House.ChatDesktop/House.ChatDesktop.csproj` | 0.1.54 | **1.0.0** |

## Evidence

### Source alignment

```
Directory.Build.props:           <Version>1.0.0</Version> / <InformationalVersion>1.0.0</InformationalVersion>
SoulCore.Host.csproj:            <Version>1.0.0</Version> / <InformationalVersion>1.0.0</InformationalVersion>
House.ChatDesktop.csproj:        <Version>1.0.0</Version> / <InformationalVersion>1.0.0</InformationalVersion>
```

### ChatDesktop Release build (default bin)

```
dotnet build House\House.ChatDesktop\House.ChatDesktop.csproj -c Release --no-incremental
Build succeeded.  0 Warning(s)  0 Error(s)  Time Elapsed 00:00:03.77
House.ChatDesktop.dll ProductVersion=1.0.0+4926585ea39ba15818fd4841532e7e1fe59ffd0f FileVersion=1.0.0.0
```

### Host Release build (sandbox — default `bin\Release` locked by running Host PID 4896)

Default-path Host build hit MSB3027 file locks on dependency DLLs (`.NET Host (4896)`). Rebuild used `VictoriaToolSandbox` per `Directory.Build.props`:

```
dotnet build SoulCore\SoulCore.Host\SoulCore.Host.csproj -c Release -p:VictoriaToolSandbox=true -p:VictoriaToolSandboxRoot=%TEMP%\victoria-prop-20.44-build --no-incremental
Build succeeded.  0 Warning(s)  0 Error(s)  Time Elapsed 00:00:03.01
SoulCore.Host.dll ProductVersion=1.0.0+4926585ea39ba15818fd4841532e7e1fe59ffd0f FileVersion=1.0.0.0
```

## Handoff — OPS

Live `/health` version still reflects the running Host process until OPS stops it and rebuilds/redeploys from these sources into the default Host bin. BED did not stop the Host (no self-deploy).
