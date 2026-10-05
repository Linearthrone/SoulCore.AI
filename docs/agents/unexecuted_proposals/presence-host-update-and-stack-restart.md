---
type: proposal
prop_id: PROP-16-presence-host-update-and-stack-restart
status: Accepted for execution
created: 2026-10-05
owner: TT-01 / PM-01
spec: docs/superpowers/specs/2026-10-05-presence-host-update-and-stack-restart-design.md
---

# PROP-16 — Presence + Host update and full stack restart

## Need

Chrome **Update** only refreshes Presence via Velopack. Host fixes still require manual `ALLSTART -RestartHost` / `-ForceRebuild`. There is no one-click full local stack restart from Presence (ALLSTOP → ALLSTART).

## Solution (Approach 1)

Extend `LocalStackControl` + `PresenceUpdateService`:

1. **Update** = git pull-if-behind → Host force-rebuild/restart → Presence Velopack apply last.
2. **Restart stack** = detached `House/scripts/restart-stack.ps1` (ALLSTOP → ALLSTART).
3. Chrome exposes both as separate buttons; Settings → Updates shows git/Host/Presence status.

## Non-goals

- Host Velopack packaging
- Tablet / Termux / My Machines workers
- Changing ALLSTART composition
- Timed auto-update

## Suggested splits

| Split | Role | Scope |
|-------|------|--------|
| 16.1 | FED-01 | `LocalStackControl` git/Host/restart APIs + chrome/Settings UI pipeline |
| 16.2 | OPS-01 | `House/scripts/restart-stack.ps1` + logging |
