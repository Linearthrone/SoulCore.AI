---
type: report
prop_id: PROP-16.2
from: OPS-01
to: PM-01
status: Pass (script landed; Windows smoke pending)
created: 2026-10-05
branch: cursor/prop16-host-update-stack-9531
---

# PROP-16.2 — OPS report

## Delivered

- `House/scripts/restart-stack.ps1` — log to `House/artifacts/restart-stack.log`, run `ALLSTOP.ps1` then `ALLSTART.ps1`, non-zero on failure.
- Presence spawns it detached via `LocalStackControl.RestartStackAsync` (`wait: false`).

## Remaining

- Windows smoke: chrome Restart stack exits Presence and ALLSTART brings Host + Presence back with `/health` OK.
