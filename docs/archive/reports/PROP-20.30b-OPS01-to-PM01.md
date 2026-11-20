---
type: report
prop_id: PROP-20.30b
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
ticket: docs/agents/tasks/PROP-20.30b-PM01-to-OPS01.md
---

# PROP-20.30b — OPS Release refresh (traits)

**Verdict: Pass**

Release Host rebuilt and restarted with PROP-20.30 trait code. `/health` OK; `GET /settings/identity` exposes `restingTraits` (9), `contextTraitCatalog` (6), and empty turn-scoped `contextTraits`.

## Serialize

- Checked for in-flight `ALLSTART` / `ForceRebuild` PowerShell before start — none active.
- Acquired `docs/agents/.ops-allstart.lock` (holder OPS-01 / PROP-20.30b); released after probes.
- Preflight: stale Host on `127.0.0.1:7710` was healthy but **identity lacked `restingTraits`** → ForceRebuild required.

## Command

```powershell
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# exit 0; log: docs/agents/reports/_allstart-prop-20.30b-release.log
```

## ALLSTART evidence (excerpt)

```
RestartHost: replacing local Victoria on :7710
RestartHost: clearing every SoulCore.Host (+ :7710 listener)
Stopping PID 22752 (dotnet) - SoulCore.Host (-RestartHost)
Stopping PID 53824 (SoulCore.Host) - SoulCore.Host (-RestartHost)
Reaped 2 leftover Host process(es) before start
  SoulCore.Host -> ...\bin\Release\net8.0\SoulCore.Host.dll
Build succeeded.
    0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
Local Victoria Host healthy.
GUI target: 127.0.0.1:7710
ALLSTART_EXIT=0
```

Note: loopback Host binds **`:7710`** (ALLSTART default). Tailscale occupies `:7700` / serves `:7710` → `127.0.0.1:7710`.

## Probes

### `GET http://127.0.0.1:7710/health`

```json
{"status":"ok","service":"SoulCore.Host","version":"0.1.54","bind":"127.0.0.1","port":7710,"charter":{"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"}}
```

Raw: `docs/agents/reports/_prop-20.30b-health.json`

### `GET http://127.0.0.1:7710/settings/identity` → `restingTraits`

| Field | Result |
| --- | --- |
| `restingTraits` | **9** rows present |
| `contextTraitCatalog` | **6** rows present |
| `contextTraits` | `[]` (turn-scoped; expected on bare GET) |
| `note` | includes `restingTraits always on; contextTraits are turn-scoped` |

`restingTraits` rows:

| id | name | weight | band | ledgerReason |
| --- | --- | --- | --- | --- |
| dominant | Dominant | 0.7 | Mid | operator |
| possessive | Possessive | 0.52 | Mid | operator-provisional-until-seed |
| affectionate | Affectionate | 0.52 | Mid | operator-provisional-until-seed |
| driven | Driven | 0.52 | Mid | operator-provisional-until-seed |
| devoted | Devoted | 0.52 | Mid | operator-provisional-until-seed |
| intense | Intense | 0.52 | Mid | operator-provisional-until-seed |
| uninhibited | Uninhibited | 0.8 | High | operator |
| ambitious | Ambitious | 0.8 | High | operator |
| proud | Proud | 0.52 | Mid | operator-provisional-until-seed |

Raw: `docs/agents/reports/_prop-20.30b-identity.json`

## Pass / Fail checklist

| Check | Result |
| --- | --- |
| Serialize (no overlapping ForceRebuild) | Pass |
| `ALLSTART -Configuration Release -RestartHost` (+ ForceRebuild) exit 0 | Pass |
| `/health` status ok | Pass |
| Identity has `restingTraits` (9) | Pass |

**Overall: Pass**
