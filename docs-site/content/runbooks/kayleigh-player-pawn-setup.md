# Kayleigh player pawn — Wave 29 setup (MyProject)

**Product decision (2026-08-04):** grounded Kayleigh body — **not** free-fly `ADefaultPawn`.  
**Project:** `C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject` (UE 5.8)  
**Map:** `/Game/Home`  
**Tickets:** BED-172 (build) · QA-173 (verify)

## Hard rules

1. **Do not reparent** `BP_MHC_Kayleigh` (or `BP_MHC_Victoria`) to `ACharacter`. MHC regenerates and clobbers edits.
2. Mirror Victoria Phase 1: separate **`BP_KayleighCharacter`** (`ACharacter`) that hosts the MetaHuman mesh from `MHC_Kayleigh`.
3. Victoria remains the bridge avatar (`VictoriaAvatar` tag). Kayleigh is the **possessed player pawn** only.
4. Prefer BP + Enhanced Input; C++ only if Voice/AudioCapture needs a tiny helper (document).

## Target hierarchy

```text
BP_KayleighCharacter (ACharacter)
├── CapsuleComponent          ← collision vs floor / walls (BlockAllDynamic or default Character)
├── CharacterMovementComponent← WalkSpeed ~150–200, Run ~450, gravity on, orient to movement
├── Mesh (SkeletalMesh)       ← MHC_Kayleigh body (metahuman_base_skel)
│     AnimClass               ← ABP_Kayleigh_Locomotion (or share ABP_Victoria_Locomotion if same skel)
│     PostProcessAnimBP       ← ABP_Body_PostProcess (keep)
├── CameraBoom (SpringArm)    ← TargetArmLength ~0–40; attach under head / eye
├── FollowCamera (Camera)     ← eye-level first person–ish (see §Camera)
├── (optional) SpringArm 3P   ← only if product wants toggle; V1 = eye-level
└── ProxVoice (AudioComponent)← spatialized, attenuation for prox chat (§Audio)
```

Actor tags: **`KayleighPlayer`** (required). Do **not** tag `VictoriaAvatar`.

## §Camera — eye level

1. Measure female MH eye height ≈ **160–168 cm** above capsule bottom (tune in PIE).
2. Preferred V1: attach `FollowCamera` to **head socket** (`head` / `FACIAL_C_FacialRoot` / MetaHuman head bone — pick the one that sits between the eyes) with small forward offset (~8–12 cm) so the mesh does not clip.
3. Alternative: SpringArm on capsule at `Z = CapsuleHalfHeight - 10` with `bUsePawnControlRotation=true`, camera relative `Z≈0`, pitch/yaw from mouse.
4. **Possessed** pawn must own the view: `Auto Possess Player = Player 0` on the placed instance **or** GameMode `DefaultPawnClass` (preferred).
5. Disable free-fly: GameMode no longer uses stock `DefaultPawn`.

## §Movement + animations

Reuse BED-115 pattern (Manny → MH retarget) unless Kayleigh mesh skeleton differs:

| Asset | Path (suggested) |
| --- | --- |
| Character | `/Game/Characters/BP_KayleighCharacter` |
| Loco ABP | `/Game/Animations/Kayleigh/Locomotion/ABP_Kayleigh_Locomotion` |
| BlendSpace | `/Game/Animations/Kayleigh/Locomotion/BS_Kayleigh_WalkRun` |
| Sequences | `AS_Kayleigh_{Idle,Walk_Fwd,Run_Fwd}` on `metahuman_base_skel` |

If Kayleigh body uses the **same** `metahuman_base_skel` as Victoria, you may assign **`ABP_Victoria_Locomotion`** temporarily to unblock walk, then duplicate/rename to Kayleigh paths for clarity.

**Input (Enhanced Input):**

| Action | Keys | Maps to |
| --- | --- | --- |
| Move | WASD | `AddMovementInput` forward/right |
| Look | Mouse | Controller yaw/pitch |
| Sprint | Shift | MaxWalkSpeed boost |
| ProxTalk | **V** (hold) | Start/stop prox voice (§Audio) |
| Jump | Space | optional; skip if house has low ceilings |

CharacterMovement defaults to document in BED report: WalkSpeed, Acceleration, Braking, capsule radius/half-height.

## §Collision

1. Capsule sized to MH body (start from Victoria Character values; retune if Kayleigh proportions differ).
2. Mesh: `Collision Enabled = Query Only` (or NoCollision) — capsule owns blocking.
3. Floor: existing UEhouse Datasmith collision (`CTF_UseSimpleAndComplex`) — Character should stand without sinking.
4. Channels: default Pawn vs WorldStatic Block; do not Block Victoria’s capsule if they need to stand close for prox chat (overlap OK; use `Pawn` response → Overlap or Ignore between Kayleigh↔Victoria if needed).

## §Audio — hear the environment

1. Ensure possessed PlayerController has **audio listener** at camera/ear (UE default when possessed).
2. Place / verify **ambient** `AmbientSound` actors in Home with **attenuation** (spatialized) so walking rooms changes what you hear.
3. Optional: `AudioListenerComponent` on head bone if camera is offset from ears.
4. Smoke: play a looping Spatialized cue in Living Room vs Bedroom — volume/pan must change when walking between them.

## §Prox chat — speak nearby

**V1 (local PIE / same machine):** proximity voice without EOS multiplayer.

1. Add **`AudioCaptureComponent`** (or project Voice interface) on `BP_KayleighCharacter`.
2. Add **`ProxVoice` `AudioComponent`**:
   - `bIsUISound = false`
   - Attenuation settings: inner radius ~150 cm, falloff ~600–1200 cm (tune)
   - Attach to head / mouth socket
3. On **ProxTalk** pressed: start capture → feed `ProxVoice` (or `UGameplayStatics::SpawnSoundAttached` stream); on release: stop.
4. Kayleigh must **not** hear herself at full volume (voice exclusion / low local gain).
5. Victoria (and any third listener) hears voice only inside attenuation range — that is “prox chat.”
6. **Out of scope for BED-172:** wiring mic audio into SoulCore STT / chat.send. File follow-up if product wants “talk to Victoria Host” via voice.

Optional debug: draw debug sphere = ProxTalk radius while V held.

## §GameMode

| Asset | Action |
| --- | --- |
| `/Game/Characters/GM_HouseVictoria` (or similar) | Create `GameModeBase` / `GameMode` |
| `DefaultPawnClass` | `BP_KayleighCharacter` |
| `DefaultEngine.ini` or Home World Settings | Set GameMode Override for `/Game/Home` → `GM_HouseVictoria` |

Do **not** change `GameDefaultMap` to Home unless OPS-approved (existing embodiment note). For PIE: open Home, set World Settings GameMode Override.

## PIE acceptance checklist

- [ ] PIE Home → possess Kayleigh (not flying DefaultPawn)
- [ ] Camera at eyes; WASD walks with idle/walk anims; capsule stops at walls/furniture
- [ ] Ambient spatial sounds change by room
- [ ] Hold V → voice audible near pawn, drops off with distance; release → silence
- [ ] Victoria avatar still present + bridge `:8888` unaffected (finder still `VictoriaAvatar`)

## Evidence for BED report

Content paths, capsule sizes, WalkSpeed, attenuation radii, input mapping asset paths, PIE notes/screenshots, any C++ touch list.
