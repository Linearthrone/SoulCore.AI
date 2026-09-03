---
type: issue
id: ISSUE-20260727-003
from: BED-01
priority: P2
status: Fixed
created: 2026-07-27
fixed: 2026-07-27
related: TASK-132, TASK-117
---

# [已修复 2026-07-27] ISSUE-20260727-003 — `move_to` still uses relative loco (teleport), not path-following

## Summary

BED-132 (`move_to` tool) wrapped `IUnrealVerbClient.LocoAsync` with relative
offsets. Absolute path-following required BED-117.

## Fix (BED-117)

1. UE: `WalkAvatarToWorldLocation` → `AAIController::MoveToLocation`; `loco`/`move_avatar_relative` relative walk; `stop` / `StopAvatarMovement`.
2. Host: `IUnrealVerbClient.MoveToAsync` / `StopAsync`; `MoveToTool` → absolute `move_to x y z`; chat keyword `stop`.
3. Debug teleport remains `move_avatar_absolute` / `set_avatar_transform`.

See `docs/agents/reports/TASK-20260726-117-BED01-to-PM01.md`.
