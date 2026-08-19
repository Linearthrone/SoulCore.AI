---
type: proposal
status: sent-to-pm
tt_id: TT-01
created: 2026-08-19
updated: 2026-08-19
title: "[TINA-main] UE reliability — Kayleigh 1P, Victoria walk/anims, one eye still"
need: Reliable Unreal output — MetaHuman animations, Victoria sight and movement, possess Kayleigh when Kurt is in the world
sent_at: 2026-08-19
pm_intake: docs/agents/tasks/TASK-20260819-195-TT01-to-PM01.md
environment: TINA-main
related: docs/agents/unexecuted_proposals/victoria-embodiment-walk-and-interact.md
---

# UE reliability — Kayleigh possess, Victoria walk/anims, eyes

## 1. Need / Want

Kurt cannot trust Unreal this session: **MetaHuman animations**, **Victoria’s sight and movement**, and **possessing his avatar (Kayleigh)** when he is active in `/Game/Home`. Script PASS / Host `success:true` has repeatedly not matched what he sees.

## 2. Goal & Success Criteria

**Sit-down gate (this PIE session, not last week’s log):**

1. Play → Kurt **is Kayleigh** (grounded, WASD, not flying `DefaultPawn`, **not Victoria**).
2. Victoria is the **other person** (AI / `VictoriaAvatar`), walks with **visible travel** (cm, not API-ok).
3. Locomotion AnimBP actually drives **both** bodies (no T-pose / slide as the Pass).
4. If she claims to see: **exactly one** Presence still — Victoria **`eye_frame`**. Empty capture is a tool error, not a described room. No live her-cam, no second UE feed in that panel.
5. Play camera: **strict first person** as Kayleigh (V1). No over-shoulder until a later ticket.
6. One WebSocket on `:8888` (no duplicate server).

Cinema MetaHuman (groom, montage library, interactables) is **after** this gate.

## 3. Context & Constraints

| Item | State |
| --- | --- |
| Product lock | Player = `BP_KayleighCharacter` / Kayleigh only. Victoria = AI. **Never** player-possess Victoria. **Do not reparent** `BP_MHC_*`. |
| TASK-191 | REX-01 **Partial** (2026-08-17): C++ GameMode/pawn on disk/P4; **live PIE still stock `DefaultPawn`**; shadow RC parameterized `py` = HTTP 400; main vs shadow split. |
| TASK-192 | `call_capture` **queued after 191 Pass**. |
| Embodiment 2026-07-26 | `victoria-embodiment-walk-and-interact.md` — MHC was `AActor`, no loco, no nav, teleport. Wrappers + nav + loco ABP landed later. |
| ISSUE-006 | API ok, **0 cm** travel; duplicate `:8888`; later BOB claimed travel after killing subsystem bind — **easy to regress**. |
| Eyes | Host `victoria_eye_capture` → UE `eye_capture` → `eye_frame`. Call camera is a **second** capture on Victoria, not Kayleigh. |
| Evidence rule | Host `success:true` without PIE class / travel / pixels = **not Pass**. |

Thinktank: STRAT, CONTRA, SYS, RISK, USER (2026-08-19).

## 4. Clarifying Q&A (answered)

Intake: anims, Victoria sight + movement, possess **his** avatar when active.

Follow-up 2026-08-19:

| Q | A |
| --- | --- |
| Camera | **Strict first person** for now. |
| Her sight | **Still images in Presence only.** There should be **only one** (Victoria `eye_frame`). No live her-cam. |
| Which Unreal | Project name **MyProject**. Assets on **Perforce on this machine**. **PIE/Play is on the shadow PC.** |

## 5. Avenues Explored

### Avenue A — Serial freeze (recommended)

1. **Finish TASK-191 on the editor Kurt Plays** — rebuild/restart so `HouseGameMode` DLL loads; **saved** Home World Settings; PIE screenshot of **possessed class = Kayleigh**. BED freeze GameMode/possess/cameras.
2. **Re-gate Victoria walk** — transform samples, cm traveled, one `:8888`.
3. **Eyes honesty** — SceneCapture on **Victoria Character wrapper** head; Host already refuses empty; Presence shows the still.
4. **Anim MVP** — existing Manny→`metahuman_base_skel` loco; DefaultSlot montages later.
5. **TASK-192** only after 191 Pass.

### Avenue B — Parallel REX/BED with contracts

Faster, historically broke (BOB possessed Victoria). Only if written forbidden list is enforced.

### Avenue C — Strip body tools until 191 Pass

Host refuses fake success. Honest, worse demo.

### Rejected

- More Live Coding / RC Python as the **reliability** strategy (CONTRA: RC 400, unsaved map, log-Pass).
- Player camera on Victoria; MHC reparent; `call_capture` before possess.
- PiP of her eyes in Kurt’s viewport (USER: two first-persons).

## 6. Recommended Route

**Four stacks. Do not mix Pass criteria.**

| Stack | Owner | Pass |
| --- | --- | --- |
| **Possess** | Dedicated UE agent (REX-01 or additional UE specialist) | PIE on **shadow PC**, MyProject from **this machine’s P4**: possessed class contains **Kayleigh**; not Victoria; not ghost; WASD; **strict 1P** |
| **Victoria loco** | Same UE seat(s) | XY travel cm + CMC velocity; AIController; Recast in **PlayWorld** |
| **Anims** | UE content specialist | Loco ABP on wrappers; optional separate Kayleigh ABP if shared instance fights |
| **Sight** | UE attach + BED Host only if Host lies | **One** Presence still = Victoria `eye_frame`. No live her-cam. Do not park call_capture in the same Presence slot. |

**Topology:** Author on **this PC’s Perforce**; **Play/PIE is the shadow PC**. Pass is only valid on **shadow Play**, not “main compiled it.”

**Staffing (Kurt):** Ticket this onto **UE-focused / dedicated Unreal agents** (REX-01 plus extra UE specialists as needed). **Do not serialize this behind main SoulCore/FED/BED/Playwright/DIGITS work.** Main Host/desktop/phone stays unblocked. BED only if Host verb honesty is still lying (`success` with 0 cm / empty PNG).

**Planes:** `:8888` = PIE body. `:30010` = editor ops (broken parameterized py on shadow). Do not add exec verbs to the body bridge.

**Persistence:** full **module rebuild + editor restart + save Home** **on the shadow**. Live Coding is not the ship path for new GameMode UCLASS.

Play viewport = **his** Kayleigh first-person camera. Presence = **one** still of **her** eyes.

## 7. Alternatives (parked)

- Cinema MH, interactables, motivated wander (old embodiment Phases 2–5).
- Over-shoulder 3P toggle.
- Remote WinRM so main can drive shadow.
- TASK-192 until 191 PIE Pass.

## 8. Risks & Kill Criteria

**Must-mitigate:** Kayleigh-only DefaultPawn; wrappers not MHC reparent; one `:8888`; motion = travel not JSON; capture on command; log pawn class + tag + Player vs AI.

**Kill:** Player possesses Victoria as the “working” PIE; MHC reparent; two servers on 8888; Pass on logs/empty PNG/`fallbackEyes`; flying DefaultPawn Pass; call capture on Kayleigh; stacking 192 before 191.

**Seat dissent:** STRAT serial vs SYS “0.5 day if Kurt at shadow keyboard” — **shadow is required** (user). USER 1P vs 3P — **1P locked**. “One still” vs TASK-192 call camera: **Presence sight = eye_frame only**; phone call camera stays a **later, separate** ticket if still wanted — do not feed it into Presence.

## 9. Open Questions for User / PM

Answered: 1P; one Presence eye still; MyProject on P4 (this machine), Play on shadow.

Still open (do not block 191):

1. **Her walk bar:** she walks a room **in front of you** on shadow, or NavMesh to a named spot is enough if the cycle looks human?
2. **TASK-192** (waist-up call camera): keep queued **off** the Presence panel, or withdraw until after sit-down reliability?

## 10. Suggested PM Handoff

**Staffing — do not slow main development.**

- Assign **dedicated Unreal agents** (REX-01 and/or additional UE specialists: Live Coding, AnimBP/MetaHuman, GameMode/possess).  
- **Do not** put these tickets on the same critical path as Playwright (193), DIGITS (194), or general BED/FED Host work. Parallel UE lane.  
- SoulCore BED/FED only for Host **honesty** (empty eye = error; no `success` without travel) — small, not a full-stack pause.

**Order (UE lane):**

1. Complete TASK-191 on **shadow Play** — rebuild/restart, save Home, **possessed-class screenshot**, strict 1P Kayleigh.  
2. Travel-cm re-gate + loco anims.  
3. Single `eye_capture` → Presence (one still).  
4. TASK-192 only after 191 Pass, and **not** as a second Presence sight feed.

- QA: sit-down on **shadow**, not pipeline log alone.
