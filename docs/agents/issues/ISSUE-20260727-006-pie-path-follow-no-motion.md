---
type: issue
issue_id: ISSUE-20260727-006
discovered: 2026-07-27
severity: P1 (Critical)
status: Pending Fix
related: TASK-118, TASK-117, ISSUE-20260727-003
wave: 26
phase: 1
---

# ISSUE-20260727-006 — PIE path-follow returns ok but Character never translates

## Problem Description

Wave 26 Phase 1 visual walk gate (QA-118) fails in live PIE: `BP_VictoriaCharacter` stands on the floor with `ABP_Victoria_Locomotion` and an `AIController`, and `HouseVictoriaBridgeBPLibrary.move_avatar_relative` returns **ok=True**, but the actor **location never changes** over ~900 transform samples (travel=0.0 cm, velocity=0.0). This is the exact Fail called out in TASK-118: bridge/API success without UE motion.

## Reproduction Steps

1. Launch UE 5.8 `MyProject.uproject` `/Game/Home` with ExecCmds running `tools/ue_nav/task118_pie_visual_walk.py`.
2. Script starts PIE (`LevelEditorSubsystem.editor_request_begin_play`).
3. Finds `BP_VictoriaCharacter` tagged `VictoriaAvatar` in PlayWorld at `(-270, 410, 98.15)`.
4. Calls `move_avatar_relative(91.44, 0, 0)` (~3 ft forward) → returns True.
5. Samples actor location / CMC velocity every 6 slate ticks until timeout (~900 ticks).

## Expected Result

Mesh translates continuously toward goal (~91 cm), walk/velocity signal present, feet stay near floor, then stop.

## Actual Result

| Check | Result |
| --- | --- |
| PIE started | Pass |
| Victoria Character on floor (Z≈98) | Pass |
| AnimClass `ABP_Victoria_Locomotion_C` | Pass |
| AIController possessed | Pass |
| `move_avatar_relative` ok | **True (API)** |
| Continuous XY travel | **Fail — 0.0 cm** |
| Velocity / walk anim signal | **Fail — vmax=0** |
| Arrive / stop | **Fail (never moved)** |

## Screenshots/Logs

Evidence: `tmpcode/qa118-evidence/`

```text
PIE ready avatar=BP_VictoriaCharacter_C_… anim=ABP_Victoria_Locomotion_C loc=(-270.0, 410.0, 98.15) ctrl=AIController_1
BPLibrary.move_avatar_relative ok=True start=(-270.0, 410.0, 98.15) goal≈(-361.44, 410.0, 98.15)
sample t=6..900 loc=(-270.0, 410.0, 98.15) speed=0.0
RESULT: FAIL timeout no continuous walk travel=0.0 vmax=0.0
```

Summary JSON: `tmpcode/qa118-evidence/task118_summary.json` (`overall=false`).

## Likely causes (for BED)

1. `MoveToLocation` returns `AlreadyAtGoal` (still treated as ok) or RequestSuccessful then path-follow never drives CMC in PIE.
2. NavMesh / Recast not available or not projected correctly in **PlayWorld** (editor bake from BED-116 may not bind to PIE navigation).
3. World-context / avatar resolve mismatch under PIE (less likely here — samples used the PIE Character ref; still worth verifying UE log `WalkAvatarToWorldLocation … result=`).

Note: BED-117 editor-world verify also saw `traveled=0` and deferred continuous motion to QA-118; PIE re-test confirms motion is still absent.

## Impact Scope

- Blocks Phase 1 exit gate (TASK-118).
- Chat / Host “walk forward 3 ft” cannot be accepted as Pass while embodiment does not move.
- Regression risk for any `loco` / `move_to` / `MoveToTool` path that only checks bridge `success:true`.

## Suggested fix direction

BED-01: In PIE Home, log `EPathFollowingRequestResult`, PathFollowing status, and whether `UNavigationSystemV1` can project start/goal; ensure MoveTo drives CMC over time; do not treat API-ok alone as visual Pass.
