---
type: report
prop_id: PROP-15.17b
prop_root: PROP-15-persona-creation-platform
from: OPS-HOME
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.17 (Pass), PROP-15.18 (Pass)
bed_report: docs/agents/reports/PROP-15.17-BED01-to-PM01.md
ops_pin: docs/agents/reports/PROP-15.18-OPSHOME-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Release Host recycle + lexi/mother pin after 15.17
---

# PROP-15.17b - Host recycle + lexi pin - Pass

## Verdict

**Pass (ops).** ChatDesktop quit (no Presence activate race). **Release** Host on `127.0.0.1:7700` with post-15.17 DLLs. Active **lexi** (Alexia), `resolvedInferenceModel=mother:latest`, `modelSource=persona`. Ready for QA stress re-run.

## Steps executed

1. **Quit ChatDesktop** - stopped PID 39792 (`House.ChatDesktop`). Confirmed not running after recycle.
2. **Release Host on :7700** - prior seat had no listener; `start-soulcore.ps1` first brought up **Debug** (`bin\Debug\net8.0`) which lacks the health shape QA expects when mixed with stale packs. Stopped Debug PID 13768. Started:
   - `SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll` via `dotnet` `--urls http://127.0.0.1:7700`
   - PID **33664**
   - DLL stamps: Host `2026-10-05 4:41:16 AM`, Core `4:41:12 AM` (PROP-15.17 build)
3. **Pin lexi + mother:latest** - pack on disk + `POST /api/personas/lexi/activate`
4. **Gate `/health`** - Pass (see table)

## Incident - partial PUT wiped lexi (recovered)

PROP-15.18 pin snippet `PUT /api/personas/lexi` with **only** `{ inferenceModel }` is a **full Upsert**, not a patch. Deserializing a partial body reset pack to C# defaults (`displayName=Blank`, empty blurb) and wrote that to `pack.json`.

- Boot log before wipe showed `active=lexi display=Alexia`.
- Restored from QA C3 verbatim dump (`docs/qa/persona-stress/results/20261005-035643/captures.jsonl`): identity seed through `## META` (LIMITS / UNCERTAINTY / META present); `inferenceModel=mother:latest`; display **Alexia** / contact **lexi**; `boundaryStrictness=0.23` (per 15.13 diagnosis). Host prompt blocks (`[Your voice]` / Tools / Computer) stripped out of blurb.
- Wiped artifact kept as `%LOCALAPPDATA%\SoulCore\personas\lexi\pack.json.wiped-by-15.17b.bak`.
- **Do not** partial-PUT packs for model pin; write full pack JSON on disk or PUT a complete body.

## Health gate (post-pin)

| Check | Result |
| --- | --- |
| Host | Release `0.1.5`, Listen `127.0.0.1:7700`, PID 33664 |
| `persona.personaId` | `lexi` |
| `persona.displayName` | `Alexia` |
| `persona.inferenceModel` | `mother:latest` |
| `inference.resolvedInferenceModel` | `mother:latest` |
| `inference.personaInferenceModel` | `mother:latest` |
| `inference.modelSource` | `persona` |
| `inference.hostModel` (fallback only) | `gemma4:latest` |
| ChatDesktop | **Stopped** |
| Ollama `mother:latest` | Present |

## Ops notes for QA / PM

- Prefer leaving ChatDesktop quit for the stress suite (15.18 race).
- `SoulCore/scripts/start-soulcore.ps1` still targets **Debug** DLL - for 15.17+ gates start **Release** explicitly (or change the script later).
- Model pin: edit `pack.json` (full fields) or full-body PUT - never inferenceModel-only PUT.

## Ask PM

1. Re-dispatch QA-01 stress (PROP-15.13 suite) against this pin + 15.17 Host.
2. Optional BED: merge-on-PUT for partial persona updates so ops/QA cannot blank a pack.
3. Optional OPS chore: point `start-soulcore.ps1` at Release for production/QA seats.