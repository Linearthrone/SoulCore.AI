---
type: issue
id: ISSUE-20260727-003
from: BED-01
priority: P2
status: Open
created: 2026-07-27
related: TASK-132, TASK-117
---

# ISSUE-20260727-003 — `move_to` still uses relative loco (teleport), not path-following

## Summary

BED-132 (`move_to` tool) wraps `IUnrealVerbClient.LocoAsync` with relative
`forward/right/up` cm offsets (`move_avatar_relative`). Absolute path-following
walk requires BED-117 (AIController + `MoveToAsync`).

## Current behavior

`MoveToTool` maps tool args `{x,y,z}` → `LocoAsync({ forward=x, right=y, up=z })`.
This is an interim teleport-style / relative step, not a continuous walk to a
world point.

## Desired

Once BED-117 lands:

1. Add `MoveToAsync(absolute x,y,z)` (or equivalent) on `IUnrealVerbClient`.
2. Switch `MoveToTool` to call path-following when available.
3. Keep relative loco available for keyword fallback / small steps if needed.

## Workaround

Documented in TASK-132 report; interim relative offset is intentional.
