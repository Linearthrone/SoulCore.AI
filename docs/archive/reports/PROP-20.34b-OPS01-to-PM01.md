---
type: report
prop_id: PROP-20.34b
from: OPS-01
to: PM-01
priority: P1
status: Pass
created: 2026-11-18
ticket: docs/agents/tasks/PROP-20.34b-PM01-to-OPS01.md
related: PROP-20.31b, PROP-20.34
---

# PROP-20.34b — OPS Host Release (hygiene)

**Verdict: Pass**

One serialized Release ALLSTART (`-RestartHost -ForceRebuild`) refreshed Host on `:7710` so live process loads PROP-20.34 F1/F2 hygiene (`ChatContextBuilder` single emotion read + shared `ScoreAppliesWhen`). Same pass also rebuilt/restarted Presence for PROP-20.31b.

## Serialize

- Checked for in-flight ForceRebuild; acquired `docs/agents/.ops-allstart.lock` (holder OPS-01 / PROP-20.34b+20.31b); released after probes.
- Cleared stale Presence holders (prior ChatDesktop / `dotnet run`) before ALLSTART so Release bin unlocks.
- Combined with **20.31b** in one ALLSTART (no overlapping rebuild).

## Command

```powershell
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# log: docs/agents/reports/_allstart-prop-20.34b-20.31b-release.clean.log
```

## ALLSTART evidence (excerpt)

```
RestartHost: replacing local Victoria on :7710
[start-soulcore :7710] starting (timeout 180s) ...
Waiting for local Victoria Host at http://127.0.0.1:7710/health ...
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
  model:  deepseek-v4.1-flash:cloud
GUI target: 127.0.0.1:7710
=== ALLSTART: House.ChatDesktop (Victoria Presence) ===
Starting House.ChatDesktop (Release)...
```

Note: loopback Host binds **`:7710`** (ALLSTART default).

## Hygiene binary markers (Release)

| Assembly | Marker | Present |
| --- | --- | --- |
| `SoulCore.Host.dll` | `LoadEmotionPreambleAndLabelSafeAsync` (F1) | **True** |
| `SoulCore.Host.dll` | `LoadEmotionLabelSafeAsync` (pre-F1) | **False** |
| `SoulCore.Core.dll` | `ScoreAppliesWhen` (F2 shared) | **True** |
| `SoulCore.Core.dll` | `ScoreContextAppliesWhen` (pre-F2) | **False** |

Host DLL / Core DLL LastWriteTime = `2026-11-18T04:09:46` (includes F1/F2 sources dated `04:08`). Live Host PID **56332** started `04:14:08` after this ALLSTART RestartHost.

## Probes

### `GET http://127.0.0.1:7710/health`

```json
{"status":"ok","service":"SoulCore.Host","version":"0.1.54","bind":"127.0.0.1","port":7710}
```

Raw: `docs/agents/reports/_prop-20.34b-health.json`

### `GET http://127.0.0.1:7710/settings/identity` → `restingTraits`

| Field | Result |
| --- | --- |
| `restingTraits` | **9** rows |
| `contextTraitCatalog` | **6** rows |
| `contextTraits` | `[]` (turn-scoped; expected on bare GET) |
| `note` | includes `restingTraits always on; contextTraits are turn-scoped` |

Raw: `docs/agents/reports/_prop-20.34b-identity.json`

## Pass / Fail checklist

| Check | Result |
| --- | --- |
| Serialize (no overlapping ForceRebuild) | Pass |
| `ALLSTART -Configuration Release -RestartHost -ForceRebuild` Host healthy | Pass |
| `/health` status ok | Pass |
| Identity has `restingTraits` (9) | Pass |
| Live Host DLL contains F1/F2 hygiene markers | Pass |

**Overall: Pass**
