---
type: report
prop_id: PROP-16.8
from: OPS-01
to: PM-01
status: Pass with notes
created: 2026-10-23
branch: pr-177
title: Creator Edition ports off the Soul_Core listeners
---

# PROP-16.8 — OPS-01 to PM-01

No commit, push, stash, or branch switch. Stayed on `pr-177`. This seat did not start a Host, did not start the bridge, and did not run `tailscale serve` enable or off.

## Port map

| Surface | Port |
| --- | --- |
| Host HTTP and WebSocket | 7720 |
| ALLSTART alternate | 7721 |
| Browser bridge and Chrome extension | 17920 |
| Tailscale TCP | 7720 → 127.0.0.1:7720 |
| Tailscale HTTPS | 8453 → 127.0.0.1:7720 |
| Voice STT / TTS (unchanged) | 8000 / 8881 |
| LastWavPublicUrl | http://127.0.0.1:7720/api/voice/last.wav |

## Left running

| Port | Pid | What |
| --- | --- | --- |
| 7700 | 7088 | Soul_Core Host, started 2026-10-21 23:10:53. Command line ends in `Soul_Core\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7700` |
| 17891 | 46640 | Browser bridge (`pythonw`), started 2026-10-21 22:19:25, script under `Soul_Core\BrowserCaptureBridge\bridge_server.py` |
| 8000 | 22184 | STT, started 2026-10-21 22:19:43 |
| 8881 | 54368 | TTS, started 2026-10-21 22:19:45 |
| 11434 | 24568 | Ollama |
| 8443, 8444, tailnet 7700 | 11324 | Existing Tailscale serve (tailscaled, started 2026-10-19 16:02:07) |

Unreal 8888 was not listening at check time. This seat did not stop it.

`ALLSTOP.ps1` was not edited and was not run. It still names 7700 / 7701 / 17891.

## Acceptance

| Line | Result |
| --- | --- |
| A default start of this tree binds Host on 7720 and the bridge on 17920 | **Pass** (defaults). Live start was not run. `17920` is not listening. |
| Presence defaults to 7720 without an env override | **Pass.** `ConnectionDefaults.DefaultPort = 7720`. `HOUSE_SOULCORE_PORT` still overrides. |
| `stop-soulcore.ps1` with no arguments targets 7720, not 7700 | **Pass** (parser). Script was not executed. |
| Tailscale enable adds 7720 and 8453 only. Off removes those two only | **Pass** (script). Enable and off were not executed. Live `tailscale serve status` still has 7700, 8443, and 8444, and has no 7720 or 8453. |
| Nothing on 7700, 17891, 8000, or 8881 was stopped | **Pass.** Same pids still listening. |
| Report lists the commands and the listener check | **Pass.** This file. |

## Note: 7720 is already listening

This seat did not bind it. At check time pid **36096** (`dotnet`) was already listening on `127.0.0.1:7720`, started **2026-10-23 12:39:33**. Command line:

```text
dotnet.exe ...\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7720
```

Left running. `SoulCore\scripts\.soulcore-host.pid` contains `36096`. Running `stop-soulcore.ps1` with no arguments would stop that process, so it was not run.

## Script defaults (parser, not executed)

```text
PARSE_OK ALLSTART.ps1
  Port = 7720
  AlternatePort = 7721
PARSE_OK SoulCore\scripts\start-soulcore.ps1
  Port = 7720
PARSE_OK SoulCore\scripts\stop-soulcore.ps1
  Port = 7720
PARSE_OK SoulCore\scripts\start-browser-bridge.ps1
  Port = 17920
PARSE_OK SoulCore\scripts\tailscale-serve-soulcore.ps1
  TcpPort = 7720
  HttpsPort = 8453
  TargetPort = 7720
```

`ALLSTART.ps1` passes `-Port` into `start-soulcore.ps1`, sets `HOUSE_SOULCORE_PORT` from the chosen port, passes `-Port 17920` to `start-browser-bridge.ps1`, and passes `-TcpPort 7720 -HttpsPort 8453 -TargetPort 7720` to `tailscale-serve-soulcore.ps1`. Off in that script is only `--tcp=$TcpPort off` and `--https=$HttpsPort off`.

## Listener check

```text
7700   127.0.0.1        pid=7088
7700   fd7a:115c:a1e0::f033:df60 pid=11324
7720   127.0.0.1        pid=36096
8000   127.0.0.1        pid=22184
8443   100.71.223.95    pid=11324
8444   fd7a:115c:a1e0::f033:df60 pid=11324
8881   127.0.0.1        pid=54368
11434  ::               pid=24568
17891  127.0.0.1        pid=46640
ALIVE 7088 dotnet
ALIVE 46640 pythonw
ALIVE 22184 python
ALIVE 54368 python
ALIVE 36096 dotnet
```

Not listening: 7721, 17920, 8453, 8888.

## Tailscale serve status (read only)

```text
|-- tcp://kaia-reimagined.tailbf9ec2.ts.net:7700 (tailnet only)
|-- tcp://100.71.223.95:7700
|-- tcp://[fd7a:115c:a1e0::f033:df60]:7700
|--> tcp://127.0.0.1:7700

https://kaia-reimagined.tailbf9ec2.ts.net:8443 (tailnet only)
|-- / proxy http://127.0.0.1:7700

https://kaia-reimagined.tailbf9ec2.ts.net:8444 (tailnet only)
|-- / proxy http://127.0.0.1:8899
```

## Build

Required command:

```text
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj --nologo
```

**Fail** (exit 1). Compile of the libraries succeeded. Copy into `SoulCore.Host\bin\Debug\net8.0` failed because pid 36096 holds the DLLs. Ending errors:

```text
error MSB3027: Could not copy "...\SoulCore.Protocol.dll" to "bin\Debug\net8.0\SoulCore.Protocol.dll". Exceeded retry count of 10. Failed. The file is locked by: ".NET Host (36096)"
error MSB3021: Unable to copy file "...\SoulCore.Protocol.dll" ... being used by another process.
error MSB3027 / MSB3021: same lock for SoulCore.Memory.dll, SoulCore.Config.dll, SoulCore.Inference.dll, SoulCore.Adapters.Ws.dll
    61 Warning(s)
    10 Error(s)
Time Elapsed 00:00:17.53
```

Follow-up, so the Host project could still be compiled without touching that locked folder:

```text
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj --nologo -c Release
Build succeeded.
    11 Warning(s)
    0 Error(s)
Time Elapsed 00:00:46.74
SoulCore.Host -> ...\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll
```

`ToolsOptions_Defaults_DesktopBrowserGatesOpen` (Release): **Passed** 1, Failed 0.

Phone fixture and spend-meter token count were not edited:

```text
[InlineData("+44 7700 900123", "+447700900123")]
meter.RecordUsage("hermes", 8000, 3000);
```

`VoiceOptions.SttUrl` remains `http://127.0.0.1:8000`. `TtsUrl` remains `http://127.0.0.1:8881`.
