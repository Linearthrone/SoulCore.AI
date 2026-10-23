---
type: report
prop_id: PROP-16.8b
prop_root: PROP-16-presence-glass-overlay
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-23
updated: 2026-10-23
branch: pr-177
ticket: docs/agents/tasks/PROP-16.8b-PM01-to-OPS01.md
---

# PROP-16.8b — ALLSTOP targets the Creator Edition ports only

**Verdict: Pass**

`ALLSTOP.ps1` was edited and then parsed with `System.Management.Automation.Language.Parser.ParseFile`. The script was not invoked. No process was stopped. Branch stayed `pr-177`. No commit, push, or stash.

`tailscale-serve-soulcore.ps1` was not edited. Teardown is still a call to that script with `-Off`.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| With no arguments, the script’s host ports are 7720 and 7721 | Pass | Param default `Ports = @(7720, 7721)`. `Stop-LocalHostOnPort -LocalPort $port` and `powershell.exe -File $StopHost -Port $port` both iterate `$Ports`. |
| The bridge stop is 17920, and 17891 is not named as a stop target | Pass | Only bridge call is `Stop-ListenersOnPort -LocalPort 17920 -Label "browser bridge"`. Token scan has no `17891`. |
| 8000 and 8881 are not stop targets | Pass | No integer literal and no token text for either port. Voice stop calls were removed. |
| 7700 and 7701 are not stop targets | Pass | Not in the param default. Token scan has no `7700` or `7701`. |
| The script was not executed | Pass | `ParseFile` only. Parser output below says `errors=0` and records that the script was not invoked. |
| Report lists the parser output | Pass | This section. |

`-KeepVoice` is still declared so existing callers still bind. It does not select a port.

## Parser output

```
=== PARSE ===
errors=0
=== PARAM DEFAULTS ===
Ports = @(7720, 7721)
KeepVoice = (no default)
KeepBrowserBridge = (no default)
=== STOP COMMANDS ===
Stop-Process :: Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
Stop-Process :: Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
Stop-Process :: Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
Stop-Process :: Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
Stop-ListenersOnPort :: Stop-ListenersOnPort -LocalPort 17920 -Label "browser bridge"
Stop-LocalHostOnPort :: Stop-LocalHostOnPort -LocalPort $port
=== INTEGER LITERALS ===
0, 2, 300, 7720, 7721, 17920
=== FORBIDDEN INTEGER LITERALS ===
none
=== TOKEN TEXT HITS FOR FORBIDDEN PORTS ===
none
=== TAILSCALE OFF ===
string=-Off
TailscaleServe assign: $TailscaleServe = Join-Path $RepoRoot "SoulCore\scripts\tailscale-serve-soulcore.ps1"
=== POWERSHELL INVOCATIONS ===
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $StopHost -Port $port 2>$null
=== SCRIPT NOT EXECUTED ===
ParseFile only; ALLSTOP.ps1 was not invoked.
```

Stop targets from that parse: **7720**, **7721** (host default and the `$port` loop), and **17920** (bridge). Integer literals `0`, `2`, and `300` are the ChatDesktop stop counter, the health timeout, and `Start-Sleep`, not ports.

Forbidden ports checked as both AST integer literals and raw token text: **7700, 7701, 17891, 8000, 8881**. Hits: none.

Tailscale string constants found: `SoulCore\scripts\tailscale-serve-soulcore.ps1` and `-Off`.

## Listeners after the edit

`Get-NetTCPConnection -State Listen` (script not run):

```
port 7720: addr=127.0.0.1 pid=36096
port 7721: no listener
port 17920: no listener
port 7700: addr=fd7a:115c:a1e0::f033:df60 pid=11324
port 7700: addr=127.0.0.1 pid=7088
port 7700: addr=100.71.223.95 pid=11324
port 7701: no listener
port 17891: addr=127.0.0.1 pid=46640
port 8000: addr=127.0.0.1 pid=22184
port 8881: addr=127.0.0.1 pid=54368
```

`Get-Process`:

```
pid 36096 alive name=dotnet
pid 7088 alive name=dotnet
```

pid 36096 is still the listener on 127.0.0.1:7720. pid 7088 is still the listener on 127.0.0.1:7700. The other checkout bridge (17891) and voice ports (8000, 8881) are still listening. 7721, 17920, and 7701 had no listener at check time.
