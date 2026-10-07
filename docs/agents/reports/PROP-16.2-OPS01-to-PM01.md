---
type: report
prop_id: PROP-16.2
from: OPS-01
to: PM-01
status: Pass (script landed; Windows smoke pending)
created: 2026-10-05
updated: 2026-10-07
branch: cursor/fix-restart-stack-9531
---

# PROP-16.2 — OPS report

## Delivered

- `House/scripts/restart-stack.ps1` — log to `House/artifacts/restart-stack.log`, run `ALLSTOP.ps1` then spawn `ALLSTART.ps1` detached.
- Presence spawns it detached via `LocalStackControl.RestartStackAsync` (`wait: false`).

## Fix 2026-10-07

- ASCII-only (PS 5.1 parse-fail on ellipsis/em-dash, same as pack-presence).
- Do **not** wait on ALLSTART — it blocks on `start-desktopgui.ps1` / `dotnet run`.
- ALLSTART stdout/stderr tee to `House/artifacts/restart-stack-allstart.*.log`.

## Remaining

- Windows smoke: chrome Restart stack exits Presence and ALLSTART brings Host + Presence back with `/health` OK.
