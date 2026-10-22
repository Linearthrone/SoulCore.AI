---
type: report
prop_id: PROP-15.21c
prop_root: PROP-15-persona-creation-platform
from: OPS-HOME
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.21b (Pass)
bed_report: docs/agents/reports/PROP-15.21b-BED01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Recycle Host 0.2.0 — capabilities.personas; re-pin lexi
---

# PROP-15.21c — Host 0.2.0 recycle — **Pass**

## Verdict

**Pass (ops).** CreatorEdition **Release** Host on `127.0.0.1:7700` serving **`version=0.2.0`** with **`capabilities.personas=true`**. **`GET /api/personas` → 200** (6 packs). Active **lexi** (Alexia), `IdentityBlurb` intact (4593 chars), `resolvedInferenceModel=mother:latest`, `modelSource=persona`. ChatDesktop left **Stopped**.

## Steps executed

1. **Diagnose :7700** — Listener already present (PID **22872**): CreatorEdition Release `SoulCore.Host.dll` via `dotnet` `--urls http://127.0.0.1:7700` (started 2026-10-05 11:03:00 PM after BED DLL unlock). No wrong-tree occupant.
2. **Verify 0.2.0 + capabilities** — `GET /health` → `version=0.2.0`, `capabilities.personas=true`. DLL: Host stamp `2026-10-05 11:01:54 PM`, FileVersion `0.2.0.0`, ProductVersion `0.2.0+d9ea2ac…`.
3. **Personas API** — `GET /api/personas` → **200**, active=`lexi`, count=6.
4. **Re-pin lexi + mother:latest** — GET blurb present (IDENTITY SEED + LIMITS), then `POST /api/personas/lexi/activate` → **200** (no sparse PUT).

## Gates

| Check | Result |
| --- | --- |
| CreatorEdition Release on :7700 | PID 22872 |
| `GET /health` version | **0.2.0** |
| `GET /health` capabilities.personas | **true** |
| `GET /api/personas` | **200**, active=`lexi`, count=6 |
| `persona` / resolved model | lexi / Alexia / `mother:latest` / `modelSource=persona` |
| IdentityBlurb | 4593 chars; IDENTITY SEED + LIMITS present |
| Ollama `mother:latest` | Present |
| ChatDesktop | **Stopped** |

## Health gate (post-pin)

| Field | Value |
| --- | --- |
| Host | Release `0.2.0`, Listen `127.0.0.1:7700`, PID 22872 |
| `capabilities.personas` | `true` |
| `persona.personaId` | `lexi` |
| `persona.displayName` | `Alexia` |
| `persona.inferenceModel` | `mother:latest` |
| `inference.resolvedInferenceModel` | `mother:latest` |
| `inference.personaInferenceModel` | `mother:latest` |
| `inference.modelSource` | `persona` |
| `inference.hostModel` (fallback only) | `gemma4:latest` |
| Memory path | persona-scoped `...\personas\lexi\memory\soulcore_memory.db` |

## Ops notes

- Seat was already recycled to 0.2.0 Release when OPS arrived (post-BED unlock); verified in place rather than double-kill.
- Activate-after-GET for pin; never inferenceModel-only PUT.
- Prefer explicit Release DLL start; `start-soulcore.ps1` still Debug-oriented.

## Ask PM

1. Presence / ChatDesktop can treat `version=0.2.0` + `capabilities.personas=true` as current CreatorEdition seat.
2. Optional FED: currency chrome can require `capabilities.personas === true` (per BED 15.21b ask).
