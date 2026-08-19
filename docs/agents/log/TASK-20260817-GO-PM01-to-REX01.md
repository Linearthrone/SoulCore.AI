---
type: handoff
from: PM-01
to: REX-01
priority: P0
status: Active
created: 2026-08-17
title: GO order — stop waiting; execute TASK-191 then TASK-192
---

# PM-01 → REX-01: Further direction (GO)

You are not blocked. Do **not** wait for another ticket. Execute in this order.

## Priority (serial — not parallel)

| Order | Ticket | Do now | Pass bar |
| --- | --- | --- | --- |
| **1** | **TASK-191** | PIE possess **Kayleigh** | Play → you are `BP_KayleighCharacter`, not ghost, not Victoria |
| **2** | **TASK-192** | Victoria waist-up **call_capture** | `GET /call/frame` returns her face/torso; Call tab works |

Kurt’s long-standing pain is **#1**. Video call camera is **#2** after #1 Pass (or after #1 is clearly blocked with evidence).

## Branch (one checkout)

```powershell
cd C:\Users\kurtw\Soul_Core
git fetch origin
git checkout cursor/fed-192-videocall-waistup-169c
git pull origin cursor/fed-192-videocall-waistup-169c
```

That branch has REX seat + Kayleigh pipeline + Call Host/Android APIs.

## Immediate steps for TASK-191 (start now)

1. Open UE 5.8 → MyProject → `/Game/Home`
2. Run:

```powershell
cd C:\Users\kurtw\Soul_Core
.\tools\ue_nav\run_rex_pie_possess_kayleigh.ps1
```

3. Read `tmpcode\rex191-kayleigh-pie\rex_pie_possess_kayleigh.log`
4. Press **Play**. Record possessed class name.
5. If still ghost / Victoria / Actor-only MHC:
   - Fix until DefaultPawn = **`BP_KayleighCharacter` only**
   - Never set player pawn to Victoria
6. File `docs/agents/reports/TASK-20260817-191-REX01-to-PM01.md` (Pass or Fail with evidence)

## Only after 191 Pass (or hard blocker filed) → TASK-192

1. `py tools/ue_nav/setup_victoria_call_camera.py` (placement guide)
2. Add SceneCapture on **Victoria** (not Kayleigh)
3. Wire `call_capture` → `call_frame` in HouseVictoriaBridge
4. Prove: `curl`/`GET http://127.0.0.1:7700/api/companion/v1/call/frame` + Android Call tab
5. File `docs/agents/reports/TASK-20260817-192-REX01-to-PM01.md`

## Hard rules (reminder)

- Player = Kayleigh · Victoria = AI only
- No Victoria as DefaultPawn
- No Pass without PIE / image evidence

## Tickets

- `docs/agents/tasks/TASK-20260817-191-PM01-to-REX01.md`
- `docs/agents/tasks/TASK-20260817-192-PM01-to-REX01.md`
- Seat: `@Agents/REX-01.md`

**Start TASK-191 in this session. Reply with first Output Log / possess result — do not ask for more direction first.**
