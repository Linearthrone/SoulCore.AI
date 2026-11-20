---
type: report
prop_id: PROP-20.31b
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
ticket: docs/agents/tasks/PROP-20.31b-PM01-to-OPS01.md
related: PROP-20.30b, PROP-20.31 / FED-20.31, PROP-20.34b
---

# PROP-20.31b — OPS Presence Release (FED-20.31)

**Verdict: Pass**

Serialized with **PROP-20.34b** on one Release ALLSTART (`-RestartHost -ForceRebuild`). Presence runs from normal `bin\Release\net8.0` against `127.0.0.1:7710` with FED-20.31 Identity trait panel markers; Host identity exposes `restingTraits`.

## Serialize

- Acquired `docs/agents/.ops-allstart.lock` (OPS-01 / PROP-20.34b+20.31b).
- Stopped prior ChatDesktop / `dotnet run` holders so Release outputs unlock.
- Single ALLSTART covered Host hygiene (20.34b) + Presence restart (20.31b).

## Command

```powershell
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# log: docs/agents/reports/_allstart-prop-20.34b-20.31b-release.clean.log
```

## Presence evidence

| Item | Value |
| --- | --- |
| Path | `House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe` |
| Presence PID | **56068** (StartTime `2026-11-18 04:15:01`) |
| `dotnet run` parent | PID **14660** |
| Host PID | **56332** (StartTime `2026-11-18 04:14:08`) |
| DLL LastWriteTime | `2026-11-18T04:05:08` (sources Identity panel `04:03`) |

### FED-20.31 DLL markers

| Marker | Present |
| --- | --- |
| `IdentityRestingTraitsText` | **True** |
| `FormatRestingTraitsBlock` | **True** |
| `restingTraits` | **True** |
| `contextTraitCatalog` | **True** |

## Probes

### `GET http://127.0.0.1:7710/health`

```text
status=ok version=0.1.54 bind=127.0.0.1 port=7710
```

Shared raw: `docs/agents/reports/_prop-20.34b-health.json` (same stack)

### `GET http://127.0.0.1:7710/settings/identity`

| Field | Result |
| --- | --- |
| `restingTraits` | **9** |
| `contextTraitCatalog` | **6** |
| `contextTraits` | `[]` |

Sample resting rows: Dominant `0.7` Mid; Possessive `0.52` Mid; Uninhibited `0.8` High.

## Pass / Fail checklist

| Check | Result |
| --- | --- |
| Serialize vs Host ForceRebuild (one ALLSTART) | Pass |
| Stop ChatDesktop; Release bin path; start vs `:7710` | Pass |
| Host `/health` ok | Pass |
| Identity has `restingTraits` for Identity panel load | Pass |
| FED-20.31 markers in live ChatDesktop DLL | Pass |

**Overall: Pass**
