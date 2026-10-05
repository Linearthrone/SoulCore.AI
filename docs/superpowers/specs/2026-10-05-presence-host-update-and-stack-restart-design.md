# Presence + Host Update and Full Stack Restart

**Date:** 2026-10-05  
**Status:** Approved for implementation planning  
**Approach:** Extend existing `LocalStackControl` + `PresenceUpdateService` (Approach 1)

## Problem

Presence chrome **Update** only refreshes the Presence app via Velopack. Host fixes still require manual `ALLSTART -RestartHost` / `-ForceRebuild`. There is no one-click full local stack restart from Presence (ALLSTOP → ALLSTART).

## Goals

1. **Update** covers **Presence and Host** (with git pull when behind).
2. **Restart stack** covers the **full local stack** (existing ALLSTOP / ALLSTART composition).
3. Chrome exposes both as separate buttons (not a menu).

## Non-goals

- Host Velopack / GitHub binary packaging for Host
- Tablet / Termux / My Machines workers
- Changing which services ALLSTART starts
- Timed auto-update (keep manual Update + existing quiet Presence check)

## UI

### Chrome (title bar)

| Control | Action |
|---------|--------|
| **Update** | Run Presence + Host update pipeline |
| **Restart stack** | Detached full-stack restart (ALLSTOP → ALLSTART) |

### Settings → Updates

- Copy states that Update covers Presence **and** Host.
- Status: git (up to date / behind N / dirty), Host version (`/health`), Presence version + feed.
- **Check for updates** refreshes status only (git fetch/status, Host `/health` version, Presence Velopack check) — does not rebuild Host or apply Presence.
- **Update now** (or chrome **Update**) runs the full apply pipeline below.
- **Restart stack** matches chrome.
- Settings → System Host Start / Stop / Restart remain Host-only.

## Update pipeline

Triggered by chrome **Update** or Settings **Update now**:

1. Resolve SoulCore repo root (`HOUSE_SOULCORE_REPO` / Settings path / existing discovery).
2. `git fetch` + compare to upstream.
   - If **behind**: confirm, then `git pull --ff-only`.
   - If **dirty** and pull would fail: stop with a clear message — no force, no hard reset. Operator may confirm **skip pull** and continue with rebuild from disk.
3. Host: `SoulCore/scripts/start-soulcore.ps1 -ForceRebuild -RestartHost`.
4. Poll `http://127.0.0.1:{port}/health`; refresh Host version in chrome / Settings.
5. Presence: existing Velopack check via `PresenceUpdateService`.
   - If an update is available: offer Download & restart (apply **last**, because it exits Presence).
   - Unpackaged / empty feed: keep today’s messages; Host update still completes.

Order is intentional: Host work finishes before Presence apply-restart.

## Restart stack

1. Confirm: “This will stop Presence and bring the full stack back up.”
2. Spawn detached `House/scripts/restart-stack.ps1` (do **not** await ALLSTOP inside the UI process — ALLSTOP kills ChatDesktop).
3. UI may show “Restarting stack…” briefly; process exits when ALLSTOP runs.
4. Script: log to `House/artifacts/restart-stack.log`, run `ALLSTOP.ps1`, then `ALLSTART.ps1` (default switches).
5. ALLSTART relaunches Presence; Host `/health` must succeed for the restart to count as healthy.

## Components

### Extend `LocalStackControl`

| Method | Behavior |
|--------|----------|
| `GetGitStatusAsync()` | `{ Behind, Ahead, Dirty, Upstream, Detail }` via `git fetch` + rev-list / `status --porcelain` |
| `PullAsync()` | `git pull --ff-only`; fail clearly if not FF or blocked |
| `UpdateHostAsync()` | `start-soulcore.ps1 -ForceRebuild -RestartHost`, then health poll |
| `RestartStackAsync()` | Start detached `House/scripts/restart-stack.ps1` (`wait: false`) |

### New script

`House/scripts/restart-stack.ps1` — orchestrates ALLSTOP → ALLSTART with logging.

### UI wiring

`MainWindow.Updates.cs` + `MainWindow.axaml`: pipeline status/toasts per step; Restart stack confirmation; busy flags so Update and Restart cannot overlap.

## Error handling

| Case | Behavior |
|------|----------|
| Missing repo root | Existing Settings guidance; no silent skip |
| Dirty / non-FF pull | Stop before Host rebuild unless skip-pull confirmed |
| Host rebuild fail | Do not apply Presence update; surface script detail |
| Unpackaged Presence | Host path still runs; Presence step explains pack/install |
| Restart script missing / spawn fail | Fail in UI; do not kill Presence |

## Testing

- Unit: git status parsing helpers (fixture dirs where practical); RestartStack launches expected script with `wait: false`.
- Manual smoke: Update with clean/behind/dirty; Restart stack returns Presence + Host healthy.

## Acceptance

1. Chrome **Update** updates Host (pull-if-behind → rebuild/restart) and Presence (Velopack when installed/available).
2. Chrome **Restart stack** detaches ALLSTOP→ALLSTART; Presence returns; Host `/health` OK.
3. Settings → Updates shows git/Host/Presence status; Check refreshes only; Update now / Restart stack match chrome.
4. Dirty/non-FF pull never force-pushes or hard-resets.
5. Unpackaged Presence still updates Host; Presence step explains install/pack requirement.
