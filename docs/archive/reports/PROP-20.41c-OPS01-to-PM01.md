---
type: report
prop_id: PROP-20.41c
from: OPS-01
to: PM-01
priority: P0
status: Completed
completed: 2026-11-18 21:31
result: Pass
---

# PROP-20.41c — OPS-01 to PM-01

Seat: home PC (OPS-HOME). Operator: LinearThrone.

**Result: Pass.** Presence Release is running and attached to Host `127.0.0.1:7710`. Host was not ForceRebuilt.

## Acceptance

| Line | Result |
| --- | --- |
| Host `:7710` healthy; no ForceRebuild unless health fails | Pass |
| Start Presence Release against `127.0.0.1:7710` | Pass (already up Release; confirmed) |
| Confirm `House.ChatDesktop` process up | Pass |
| Report `docs/agents/reports/PROP-20.41c-OPS01-to-PM01.md` | Pass |

## Host (untouched)

| Field | Value |
| --- | --- |
| Health | `GET http://127.0.0.1:7710/health` → **HTTP 200**, `"status":"ok"`, `"port":7710`, `"version":"0.1.54"` |
| Process | `dotnet` pid **4896** |
| Image | `SoulCore\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710` |
| Start | 2026-11-18 21:22:46 |
| ForceRebuild | **not used** |
| RestartHost | **not used** |

Evidence snippet (keys present): `status,service,version,bind,port,phase,tasks,ws,memory,inference,soulLoop,unreal,presence,tools,charter,safety,sms`. `presence.currentActivity` was live (`Between talks · quiet`).

No `-ForceRebuild`. No `stop-soulcore.ps1`. No Host kill.

## Presence (ChatDesktop)

At OPS handoff, `House.ChatDesktop` was **already running** as the repo Release build (ALLSTART abort left Host up; desk had been relaunched before this seat finished probes). Desired end-state already met — did **not** kill/relaunch (would drop live WS). Verified process + sockets instead of a redundant start.

| Field | Value |
| --- | --- |
| Pid | **27516** |
| Responding | True |
| Start | 2026-11-18 21:30:32 |
| Image | `House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe` |
| DLL LastWrite | 2026-11-18 21:18:50 |
| WS to Host | multiple **Established** TCP `127.0.0.1:* → 127.0.0.1:7710` |

`presence-startup.log` (tail):

```
2026-11-18 21:30:32.603 Main enter args=0 base=c:\Users\kurtw\.house_victoria\SoulCore.AI\House\House.ChatDesktop\bin\Release\net8.0\
2026-11-18 21:30:32.665 Starting Avalonia lifetime
2026-11-18 21:30:33.072 Framework initialization completed
2026-11-18 21:30:33.377 MainWindow created
2026-11-18 21:30:33.521 MainWindow opened at 2718,0 size 1900x1108 state=Normal
```

## Commands / evidence (PowerShell)

```powershell
Invoke-WebRequest http://127.0.0.1:7710/health   # 200 ok
Get-Process House.ChatDesktop                    # pid 27516
Get-NetTCPConnection -OwningProcess 27516 | ? RemotePort -eq 7710   # Established
```

## Notes

- Did not run `ALLSTART.ps1` or `-ForceRebuild`.
- Did not print `SoulCore/.env`.
- `ws-companion-auth-probe.ps1` hit a pre-existing parser error in the script; Presence↔Host proven via Established TCP to `:7710` + `/health` instead.

**Pass.**
