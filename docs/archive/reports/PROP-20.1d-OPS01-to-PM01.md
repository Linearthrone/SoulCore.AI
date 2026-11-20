---
type: report
prop_id: PROP-20.1d
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
updated: 2026-11-17
title: Release Host+Presence after SixLabors license present
machine: home-pc
configuration: Release
---

# PROP-20.1d — OPS Release retry

**Result: Pass**

Release Host + Release Presence are live on `:7710` after `sixlabors.lic` satisfied ImageSharp. ALLSTART `-Configuration Release -RestartHost -ForceRebuild` completed Host rebuild/start; Presence confirmed on `bin\Release`.

## Acceptance

| Criterion | Result |
| --- | --- |
| Release Host build succeeds (license OK) | **Pass** — Host project + solution Release builds, 0 Error(s); no ImageSharp license failure |
| `ALLSTART -Configuration Release -RestartHost` (+ ForceRebuild if locks) | **Pass** — first attempt hit Debug Presence DLL lock; ForceRebuild after ops script fix started Release Host PID **2544** |
| Probe `/health` | **Pass** — `status=ok`, `service=SoulCore.Host`, memory open |
| Probe `/settings/identity` (books + anchors) | **Pass** — 18 anchors, books populated |
| Probe `/journals/books` | **Pass** — count=7 |
| Presence is Release (not Debug) | **Pass** — `House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe` PID **57260** |
| Report with command evidence | **Pass** — this file |

## Ops script unblock (required for Pass)

`start-soulcore.ps1` previously hard-coded `-c Debug` and `bin\Debug\…SoulCore.Host.dll`, so `ALLSTART -Configuration Release` only flipped Presence. OPS patched:

- `SoulCore/scripts/start-soulcore.ps1` — `-Configuration` (default **Release**), build/run that config; stop `House.ChatDesktop` before solution build to clear DLL locks
- `ALLSTART.ps1` — forwards `-Configuration` into `start-soulcore.ps1`

## Commands

```powershell
# Gate
Test-Path .\sixlabors.lic   # True (184 bytes)

# Standalone Host Release (pre-ALLSTART)
dotnet build SoulCore\SoulCore.Host\SoulCore.Host.csproj -c Release
# Build succeeded. 4 Warning(s) CA1416 only. 0 Error(s).

# Attempt 1 (failed — start-soulcore still Debug + Presence locked Protocol.dll)
.\ALLSTART.ps1 -Configuration Release -RestartHost
# MSB3027 locked by House.ChatDesktop (42080) on Debug Protocol.dll → Host never healthy

# Attempt 2 (after Configuration wiring)
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# Building SoulCore (Release)... Build succeeded. 0 Warning(s). 0 Error(s).
# Starting SoulCore.Host on http://127.0.0.1:7710 ... PID: 2544
# Local Victoria Host healthy.
# Starting House.ChatDesktop (Release)...
```

## Evidence

### License

```text
C:\Users\kurtw\.house_victoria\SoulCore.AI\sixlabors.lic    Length=184
```

### Host Release build (standalone)

```text
SoulCore.Host -> ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll
Build succeeded.
    4 Warning(s)
    0 Error(s)
Time Elapsed 00:00:04.07
```

### ALLSTART Release solution rebuild

```text
Building SoulCore (Release)...
  SoulCore.Host -> ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll
  House.ChatDesktop -> ...\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.05
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 2544
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
  model:  deepseek-v4.1-flash:cloud
Starting House.ChatDesktop (Release)...
```

### Host process

```text
PID 2544
dotnet ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710
Hosting environment: Production
Content root path: ...\SoulCore.Host\bin\Release\net8.0
SqliteMemorySession ready at C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
SoulCore.Host listening on http://127.0.0.1:7710
```

### Presence process

```text
PID 57260
Path: ...\House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe
```

### `GET http://127.0.0.1:7710/health`

```text
status=ok
service=SoulCore.Host
version=0.1.54
bind=127.0.0.1
port=7710
memory.open=True
memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
inference.provider=ollama-cloud
inference.model=deepseek-v4.1-flash:cloud
charter.anchors=18
charter.locked=18
charter.fullyLocked=True
charter.mode=locked
```

### `GET http://127.0.0.1:7710/settings/identity`

```text
displayName=Victoria
charter.anchors=18 locked=18 fullyLocked=True mode=locked
identityAnchors=8
anchors=18
books: attributes=3 personality=5 constitution=5 compass=5
sample: id=11 title=Name book=attributes; id=18 title=Promise book=compass
```

### `GET http://127.0.0.1:7710/journals/books`

```text
count=7
books=feeling, animation, environment, life_hacks, thoughts, wants, dreams
```

## deploy_history / version docs

- No `deploy_history.json` and no `docs/版本变更记录.md` in this tree.
- Host+Presence Release restart only (version still `0.1.54` from `/health`) — no ship changelog bump performed (same stance as prior Host-only OPS reports).

## Notes

- Unreal WS still unavailable (`unreal.connected=false`) — Host continues; not a Release gate failure.
- Leisure model `qwen2.5:1.5b` not pulled — R&R quiet; unrelated to this ticket.
- ALLSTART parent shell remains open while Presence (`dotnet run`) is foreground — expected.
