---
type: report
prop_id: PROP-15.21
prop_root: PROP-15-persona-creation-platform
from: OPS-HOME
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Recycle :7700 off legacy Soulcore.AI Host; CreatorEdition Release with /api/personas
---

# PROP-15.21 - CreatorEdition Host recycle - Pass

## Verdict

**Pass (ops).** Stopped legacy Host from wrong tree (`C:\Users\kurtw\Soulcore.AI\...` Debug, version `0.1.5`, `GET /api/personas` → 404). Started **CreatorEdition Release** Host on `127.0.0.1:7700`. **`GET /api/personas` → 200** (6 packs). Active **lexi** (Alexia), `IdentityBlurb` intact (4593 chars), `resolvedInferenceModel=mother:latest`, `modelSource=persona`. ChatDesktop left **Stopped**. Presence switcher can refresh against this seat.

## Steps executed

1. **Diagnose :7700** - PID **2928** was `dotnet ...\Soulcore.AI\SoulCore.AI\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll` (not CreatorEdition). Health: `version=0.1.5`, memory path legacy `...\SoulCore\memory\soulcore_memory.db`, model `gemma4:latest`. `GET /api/personas` → **404**.
2. **Stop** PID 2928; confirmed port free.
3. **Build + start CreatorEdition Release**
   - `dotnet build SoulCore\SoulCore.Host\SoulCore.Host.csproj -c Release` (succeeded; 1 CA1416 warning)
   - Started `SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll` via `dotnet` `--urls http://127.0.0.1:7700`
   - Listener PID **3728**
   - DLL stamps: Host `2026-10-05 11:00:08 PM`, Core `11:00:04 PM`
   - Logs: `docs/agents/reports/_ops-logs/prop-15.21-host-stdout.log`
4. **Verify** `/api/personas` → **200**; pack list includes lexi.
5. **Pin lexi + mother:latest** - boot already restored active=lexi; confirmed GET blurb, then `POST /api/personas/lexi/activate` (no sparse PUT).

## Gates

| Check | Result |
| --- | --- |
| Prior Host tree | Wrong repo (`Soulcore.AI` Debug) - stopped |
| CreatorEdition Release on :7700 | PID 3728 |
| `GET /health` version | `0.1.5` (matches csproj `<Version>`; not a stale-build signal) |
| Memory path | persona-scoped `...\personas\lexi\memory\soulcore_memory.db` |
| `GET /api/personas` | **200**, active=`lexi`, count=6 |
| `persona` / resolved model | lexi / Alexia / `mother:latest` / `modelSource=persona` |
| IdentityBlurb | 4593 chars; IDENTITY SEED + LIMITS present |
| Ollama `mother:latest` | Present |
| ChatDesktop | **Stopped** |

## Version note for BED / PM

csproj still ships `<Version>0.1.5</Version>`. Live health correctly reports **0.1.5** after rebuild; the 404 was **wrong binary tree**, not a version-string lag. Optional: bump Host version on next persona-platform ship so seats are distinguishable from legacy `Soulcore.AI` 0.1.5.

## Ops notes

- Do not start Host from `C:\Users\kurtw\Soulcore.AI\...` when working CreatorEdition Presence/personas.
- Prefer explicit Release DLL start (as in 15.17b–15.20b); `start-soulcore.ps1` still Debug-oriented.
- Activate-after-GET for pin; never inferenceModel-only PUT.

## Ask PM

1. Presence / ChatDesktop can open against this seat for switcher validation.
2. Optional BED: Host version bump so CreatorEdition ≠ legacy 0.1.5 in `/health`.
