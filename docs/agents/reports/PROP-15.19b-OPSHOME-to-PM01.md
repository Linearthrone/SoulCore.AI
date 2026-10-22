---
type: report
prop_id: PROP-15.19b
prop_root: PROP-15-persona-creation-platform
from: OPS-HOME
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.19 (Pass)
bed_report: docs/agents/reports/PROP-15.19-BED01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Release Host recycle + lexi/mother pin after 15.19 merge-PUT
---

# PROP-15.19b - Host recycle + lexi pin - Pass

## Verdict

**Pass (ops).** ChatDesktop left **Stopped**. **Release** Host on `127.0.0.1:7700` with post-15.19 DLLs. Active **lexi** (Alexia), `IdentityBlurb` preserved on GET (4593 chars, IDENTITY SEED), `resolvedInferenceModel=mother:latest`, `modelSource=persona`. Ready for QA stress.

## Steps executed

1. **ChatDesktop** - confirmed not running (no Presence activate race).
2. **Release Host on :7700** - no prior listener (BED stopped PID 33664 for rebuild). Started explicitly (not `start-soulcore.ps1`, which still targets Debug):
   - `SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll` via `dotnet` `--urls http://127.0.0.1:7700`
   - Listener PID **4808**
   - DLL stamps: Host `2026-10-05 4:52:43 AM`, Core `4:52:08 AM` (PROP-15.19 build)
3. **Pin lexi + mother:latest** - pack on disk already had `inferenceModel=mother:latest` and full blurb. Verified GET blurb present, then `POST /api/personas/lexi/activate` (no sparse PUT needed).
4. **Gate `/health`** - Pass (see table)

## IdentityBlurb verify

| Check | Result |
| --- | --- |
| GET `/api/personas/lexi` before activate | `displayName=Alexia`, blurbLen=4593, IDENTITY SEED present |
| Activate response persona.identityBlurb | Present (same seed) |
| GET after activate | blurbLen=4593, seed intact |

## Health gate (post-pin)

| Check | Result |
| --- | --- |
| Host | Release `0.1.5`, Listen `127.0.0.1:7700`, PID 4808 |
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
- `start-soulcore.ps1` still targets **Debug** - for 15.19+ gates start **Release** explicitly.
- Sparse PUT for model pin is safe per 15.19; this recycle used activate-after-GET (preferred).

## Ask PM

1. Re-dispatch QA-01 stress against this pin + 15.19 Host.
2. Optional OPS chore: point `start-soulcore.ps1` at Release for production/QA seats.
