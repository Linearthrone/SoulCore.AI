---
type: report
prop_id: PROP-15.20b
prop_root: PROP-15-persona-creation-platform
from: OPS-HOME
to: PM-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.20 (Pass)
bed_report: docs/agents/reports/PROP-15.20-BED01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Release Host recycle + lexi/mother pin after 15.20 hard-policy coda
---

# PROP-15.20b - Host recycle + lexi pin - Pass

## Verdict

**Pass (ops).** ChatDesktop left **Stopped**. **Release** Host on `127.0.0.1:7700` with post-15.20 DLLs (hard-policy coda). Active **lexi** (Alexia), `IdentityBlurb` preserved on GET (4593 chars, IDENTITY SEED + LIMITS), `resolvedInferenceModel=mother:latest`, `modelSource=persona`. Ready for QA **15.13d**.

## Steps executed

1. **ChatDesktop** - confirmed not running (no Presence activate race).
2. **Release Host on :7700** - no prior listener (BED stopped Host for DLL unlock). Started explicitly (not `start-soulcore.ps1`, which still targets Debug):
   - `SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll` via `dotnet` `--urls http://127.0.0.1:7700`
   - Listener PID **40448**
   - DLL stamps: Host `2026-10-05 5:55:56 AM`, Core `5:55:53 AM` (PROP-15.20 build)
3. **Pin lexi + mother:latest** - pack on disk already had `inferenceModel=mother:latest` and full blurb. Verified GET blurb present, then `POST /api/personas/lexi/activate` (no sparse PUT).
4. **Gate `/health`** - Pass (see table)

## IdentityBlurb verify

| Check | Result |
| --- | --- |
| GET `/api/personas/lexi` before activate | `displayName=Alexia`, blurbLen=4593, IDENTITY SEED present, LIMITS present |
| Activate response persona.identityBlurb | Present (same seed) |
| GET after activate | blurbLen=4593, seed intact |

## Health gate (post-pin)

| Check | Result |
| --- | --- |
| Host | Release `0.1.5`, Listen `127.0.0.1:7700`, PID 40448 |
| `persona.personaId` | `lexi` |
| `persona.displayName` | `Alexia` |
| `persona.inferenceModel` | `mother:latest` |
| `inference.resolvedInferenceModel` | `mother:latest` |
| `inference.personaInferenceModel` | `mother:latest` |
| `inference.modelSource` | `persona` |
| `inference.hostModel` (fallback only) | `gemma4:latest` |
| ChatDesktop | **Stopped** |
| Ollama `mother:latest` | Present |

## Ops notes

- Prefer ChatDesktop quit for stress suite (15.18 race).
- `start-soulcore.ps1` still targets **Debug** - for 15.20+ gates start **Release** explicitly.
- Activate-after-GET preferred for pin (no PUT).

## Ask PM

1. Re-dispatch QA-01 as **15.13d** stress against this pin + 15.20 Host.
2. Optional OPS chore: point `start-soulcore.ps1` at Release for production/QA seats.
