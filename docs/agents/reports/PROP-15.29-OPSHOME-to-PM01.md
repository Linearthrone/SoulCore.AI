---
type: report
prop_id: PROP-15.29
prop_root: PROP-15-persona-creation-platform
from: OPS-HOME
to: PM-01
priority: P0
status: Pass
created: 2026-10-21
updated: 2026-10-21
depends_on: PROP-15.21c (Pass)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Recycle :7700 to CreatorEdition Host 0.2.0 — ForceRebuild Release
---

# PROP-15.29 — Host 0.2.0 recycle — **Pass**

## Verdict

**Pass (ops).** CreatorEdition **Release** Host on `127.0.0.1:7700` serving **`version=0.2.0`** with **`capabilities.personas=true`**. **`GET /api/personas` → 200** (6 packs). Active **lexi** (Alexia), IdentityBlurb intact (4593 chars), `resolvedInferenceModel=mother:latest`, `modelSource=persona`. ChatDesktop left **Running** (PID 38688).

## Root cause

ALLSTART / `start-soulcore.ps1` had reused a **stale Debug** DLL from this same tree:

- Path: `...\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll`
- Health: `version=0.1.5`, no `capabilities.personas`, effective model `gemma4:latest` (not installed)
- Listener PID **45208** (`dotnet` Debug Host)

Not a wrong-repo tree — wrong **configuration + skip-rebuild** path.

## Steps executed

1. **Stop :7700** — Killed Debug Host PID **45208**. ChatDesktop PID **38688** left alone (reconnected after recycle).
2. **Release build** — `dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj -c Release` → DLL FileVersion `0.2.0.0`, ProductVersion `0.2.0+d9ea2ac…`, stamp `2026-10-21 21:10:23`.
3. **Start Release Host** — `dotnet` + Release `SoulCore.Host.dll --urls http://127.0.0.1:7700` (loaded `SOULCORE_*` from `SoulCore/.env`). New PID **35216**.
4. **Gates** — `GET /health` → `0.2.0` + `capabilities.personas=true`; `GET /api/personas` → **200**.
5. **Re-pin lexi** — GET blurb present, then `POST /api/personas/lexi/activate` → **200**; resolved `mother:latest` / `modelSource=persona`.

## Gates

| Check | Result |
| --- | --- |
| CreatorEdition Release on :7700 | PID **35216** |
| `GET /health` version | **0.2.0** |
| `GET /health` capabilities.personas | **true** |
| `GET /api/personas` | **200**, active=`lexi`, count=6 |
| `persona` / resolved model | lexi / Alexia / `mother:latest` / `modelSource=persona` |
| IdentityBlurb | 4593 chars; IDENTITY SEED + LIMITS present |
| Ollama `mother:latest` | Present (gemma4 **not** installed; not used as resolved) |
| ChatDesktop | **Running** PID **38688** (CreatorEdition Release) |

## Health gate (post-pin)

| Field | Value |
| --- | --- |
| Host | Release `0.2.0`, Listen `127.0.0.1:7700`, PID 35216 |
| Command line | `...\bin\Release\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7700` |
| `capabilities.personas` | `true` |
| `persona.personaId` | `lexi` |
| `persona.displayName` | `Alexia` |
| `inference.model` / `resolvedInferenceModel` | `mother:latest` |
| `inference.personaInferenceModel` | `mother:latest` |
| `inference.modelSource` | `persona` |
| `inference.hostModel` (fallback only) | `gemma4:latest` |
| Memory path | persona-scoped `...\personas\lexi\memory\soulcore_memory.db` |

## Ops notes

- Prefer explicit **Release** DLL start for PROP-15 closeout. `start-soulcore.ps1` still defaults to **Debug** and skips rebuild when Debug DLL exists — that is how `0.1.5` returned after ALLSTART.
- Activate-after-GET for pin; never inferenceModel-only PUT.
- `hostModel=gemma4:latest` remains Host appsettings fallback only; chat resolves **persona** `mother:latest`.

## Ask PM

1. Presence / ChatDesktop can treat `version=0.2.0` + `capabilities.personas=true` as current CreatorEdition seat.
2. Optional follow-up: teach ALLSTART / `start-soulcore.ps1` to prefer Release (or ForceRebuild when csproj Version ≠ live health version) so Debug `0.1.5` cannot reseat `:7700`.
