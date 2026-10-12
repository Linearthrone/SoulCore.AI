---
type: report
prop_id: PROP-17.1
from: BED-01
to: PM-01
status: Partial
created: 2026-10-12
updated: 2026-10-12
---

# PROP-17.1 — Ledger park/suspend + host rejects + Host Stop — Partial

SoulCore ledger, park/probe, and host rejects landed. Presence UI “Starting Host…” clear / Stop-enabled-during-start stays in House (out of BED scope).

## Done

1. `victoria_tasks` migration 008: statuses `todo|in_progress|done|blocked|parked|suspended`; `surface`, `park_reason`, `park_until`, identical-failure fields, notes. No delete API.
2. `IVictoriaTaskStore` / repo / `task_*` (+ `task_release` / `task_suspend`) updated; create defaults to `suspended` unless `ready=true`.
3. `assigned-work.json` is `{ runningId }` only; legacy active/waiting migrates into a ledger row + pointer.
4. `AssignedWorkRunner` resumes only `in_progress` running id; never schedules parked/suspended; tool name ≠ progress; 3 identical failures → park 45m; probe then repark 120m.
5. Model-minted `confirmed` stripped on email send/delete and MT4 writes; `IOperatorConsentGate` + Kayleigh “yes” / `POST /settings/consent`.
6. `POST /settings/tools` requires companion token (same filter as email settings).
7. `guest_shell` refuses apt/snap/flatpak install and wget|bash / curl|bash in every mode.
8. `start-soulcore.ps1` start timeout force-kills stuck Host; `stop-soulcore.ps1` already force-kills listeners/pid.

## Partial / blocker

- House Avalonia: keep Stop enabled while “Starting Host…” and clear the busy flag on timeout — FED / House sibling (SCOPE forbade `House/**`).

## Evidence

- `dotnet build SoulCore.Protocol.Tests` — success
- Targeted tests: 113 passed (`Prop17*`, `TaskTools*`, `WorkContinuation*`, `GuestWorkspace*`, `EmailTools*`, `Mt4Tools*`, journal)

## Files (SoulCore)

- Memory migrations 008 + task/SOP/journal store updates
- `AssignedWorkStore` / `AssignedWorkRunner` / `AssignedWorkHostedService`
- `OperatorConsentGate`, email/MT4 write paths, `GuestPathGuard`, Host settings auth
- `scripts/start-soulcore.ps1`
- Protocol.Tests updates + `Prop17ParkAndSopTests`
