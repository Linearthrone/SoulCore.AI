---
type: report
prop_id: PROP-16.1
from: FED-01
to: PM-01
status: Pass (code; Windows smoke pending)
created: 2026-10-05
branch: cursor/prop16-host-update-stack-9531
---

# PROP-16.1 — FED report

## Delivered

- `LocalStackControl`: `GetGitStatusAsync`, `PullAsync` (`--ff-only`), `UpdateHostAsync` (`-ForceRebuild -RestartHost` + health poll), `RestartStackAsync` (detached).
- Chrome **Update** → full pipeline; chrome **Restart stack** → confirm + detached script.
- Settings → Updates: git status box, Check (status only), Update now, Restart stack.
- Presence 0.1.9 bump; copy updated so Host is part of Update path.
- Unit tests for git parse helpers + missing restart script.

## Remaining

- Windows Home PC smoke: Update (clean/behind/dirty) + Restart stack returns Presence + Host `/health`.
