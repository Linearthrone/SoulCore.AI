---
type: report
prop_id: PROP-18.6c
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-19
seat: OPS-HOME
depends_on: PROP-18.6b
---

# PROP-18.6c — OPS report (Release Host rebuild — residue APIs)

## Verdict: **Pass**

Release ALLSTART with `-RestartHost -ForceRebuild` rebuilt Host on `:7710`. Residue MapGets now respond non-404: `/activities`, `/journals/topic`, `/presence/while-you-were-gone` all **HTTP 200** (empty payloads ok).

## Commands / evidence

```text
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild -SkipVoice -SkipPlaywrightInstall
# tee: docs/agents/reports/_allstart-prop-18.6c-release.log
```

Host start excerpt:

```text
RestartHost: replacing local Victoria on :7710
RestartHost: clearing every SoulCore.Host (+ :7710 listener)
Stopping PID 58832 (dotnet) - SoulCore.Host (-RestartHost)
Building SoulCore (Release)...
Build succeeded.  4 Warning(s)  0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 43372
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

### Probe output (paste)

```text
GET /health -> HTTP 200
{"status":"ok","service":"SoulCore.Host","version":"1.0.0","bind":"127.0.0.1","port":7710,"phase":1,"tasks":{"count":0,"runningId":null},"idleParkedBudget":{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null},"ws":{"path":"/ws","url":"ws://127.0.0.1:7710/ws"},"memory":{"open":true,"path":"C:\\Users\\kurtw\\AppData\\Local\\SoulCore\\memory\\soulcore_memory.db",...}

GET /activities?limit=3 -> HTTP 200
{"count":0,"activities":[]}

GET /journals/topic?subject=optics&limit=3 -> HTTP 200
{"subject":"optics","bookId":"thoughts","count":0,"entries":[]}

GET /presence/while-you-were-gone?limit=3 -> HTTP 200
{"since":"2026-11-18T22:29:36.564Z","empty":true,"activities":[],"activityCount":0,"journalCrumbs":[],"journalCount":0}
```

DLL rebuild timestamp: `SoulCore.Host.dll` mtime `2026-11-19T05:19:35-05:00`.

## Checklist

| Check | Result |
| --- | --- |
| `ALLSTART -Configuration Release -RestartHost -ForceRebuild` Host healthy | Pass |
| `GET /health` status=ok | Pass |
| `GET /activities?limit=3` non-404 | Pass (200 empty) |
| `GET /journals/topic?subject=optics&limit=3` non-404 | Pass (200 empty) |
| `GET /presence/while-you-were-gone?limit=3` non-404 | Pass (200 empty) |

## deploy_history / version docs

- No `deploy_history.json` and no `docs/版本变更记录.md` in this tree (same as prior PROP-20.x / 18.6b OPS notes). Not created here — product version SoT remains live `/health` (`1.0.0`).

## Notes

- Voice skipped (`-SkipVoice`); Playwright verify skipped (`-SkipPlaywrightInstall`).
- Presence: ALLSTART may still be on ChatDesktop `dotnet run` after Host healthy — Host/residue-API acceptance for **18.6c** is green regardless.
