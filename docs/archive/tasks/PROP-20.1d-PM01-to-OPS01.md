---
type: task
prop_id: PROP-20.1d
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-17
updated: 2026-11-17
title: Release Host+Presence after SixLabors license present
depends_on: PROP-20.1c, PROP-20.24
unblocks: production ALLSTART Release for Wave A + step 4 + resting weights
---

# PROP-20.1d — OPS Release retry

**Activate:** `@Agents/OPS-01.md` — **unheld 2026-11-17**.

## Gate

`sixlabors.lic` is at repo root (gitignored). PROP-20.1c plumbing maps it. Operator resting weights (20.24) are in tree.

## Action

1. Confirm **Release** Host build succeeds (paste output) — license file must satisfy ImageSharp.
2. `ALLSTART.ps1 -Configuration Release -RestartHost` (ForceRebuild if DLL locks).
3. Probe `/health`, `/settings/identity` (books + anchors), `/journals/books`.
4. Confirm Presence Release is running (not Debug).
5. Report: `docs/agents/reports/PROP-20.1d-OPS01-to-PM01.md`

## Status

**Pending** — handed off.
