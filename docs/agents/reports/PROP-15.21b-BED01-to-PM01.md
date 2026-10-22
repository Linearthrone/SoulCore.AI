---
type: report
prop_id: PROP-15.21b
prop_root: PROP-15-persona-creation-platform
from: BED-01
to: PM-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.21 (Pass)
ops_report: docs/agents/reports/PROP-15.21-OPSHOME-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Host Version 0.2.0 + capabilities.personas on /health
---

# PROP-15.21b — Host version honesty — **Pass**

## Verdict

**Pass.** CreatorEdition Host now ships **`0.2.0`** (`Version` + `InformationalVersion`) and `/health` includes **`capabilities.personas: true`**. Presence can distinguish this tree from pre-persona Hosts that still report `0.1.5` (and omit `capabilities`).

## Context (OPS 15.21)

OPS Pass confirmed personas API works on CreatorEdition Release; live `/health.version` was still **`0.1.5`** because csproj had not been bumped (not a stale-binary lie). This PROP makes the version string honest for the persona platform.

## Changes

### Version bump (`SoulCore.Host.csproj`)

| Field | Before | After |
| --- | --- | --- |
| `<Version>` | `0.1.5` | `0.2.0` |
| `<InformationalVersion>` | `0.1.5` | `0.2.0` |

`HostBuildVersion.Current` reads InformationalVersion → `/health.version` becomes `0.2.0` after recycle.

### `/health` capabilities

Added cheap static object (no I/O):

```json
"capabilities": { "personas": true }
```

Pre-persona / wrong-tree Hosts lack this key → Presence can treat missing/`false` as outdated alongside `/api/personas` 404.

## Files changed

### Modified
- `SoulCore/SoulCore.Host/SoulCore.Host.csproj` — Version / InformationalVersion → `0.2.0`
- `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` — `capabilities.personas = true` on `/health`
- `SoulCore/SoulCore.Protocol.Tests/HostBuildVersionTests.cs` — asserts Current starts with `0.2.`
- `docs/agents/tasks/PROP-15.21b-PM01-to-BED01.md` — status → Pass

## Evidence

```text
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj -c Release
Build succeeded.  0 Warning(s)  0 Error(s)

dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Release --filter "FullyQualifiedName~HostBuildVersion"
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: 118 ms
```

(Stopped Release Host PID **3728** briefly so DLLs could copy — same unlock pattern as 15.17 / 15.19 / 15.20.)

## Ops note — **required**

Release Host on `:7700` was stopped for DLL unlock and is **not** restarted by BED.

**OPS-HOME must recycle** CreatorEdition Release Host on `127.0.0.1:7700`, then verify:

| Check | Expected |
| --- | --- |
| `GET /health` → `version` | `0.2.0` |
| `GET /health` → `capabilities.personas` | `true` |
| `GET /api/personas` | 200 (unchanged) |
| Active pack | re-pin **lexi** / `mother:latest` if needed |

Until recycle, live seat still serves the previous binary (or nothing).

## Ask PM

1. Dispatch OPS recycle (or fold into next OPS seat pin).
2. Optional FED: Presence currency chrome can treat `capabilities.personas !== true` as Host outdated (in addition to `/api/personas` 404).
