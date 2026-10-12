---
type: task
prop_id: PROP-17.1
prop_root: PROP-17-victoria-between-talks
from: PM-01
to: BED-01
priority: P0
status: Partial
created: 2026-10-12
updated: 2026-10-12
wave: between-talks
title: Ledger park/suspend + host rejects + Host Stop force-kill
depends_on: none
blocks: PROP-17.3 (hard); PROP-17.4 activity-band parked phrases (soft)
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
intake: docs/agents/tasks/PROP-17-TT01-to-PM01.md
urgency: same-day — operator needs Victoria park-capable TODAY for bug-out; workable TOMORROW
environment: TINA-main
---

# PROP-17.1 — Ledger, park, suspend, host rejects, Host Stop

**Activate:** `@Agents/BED-01.md` then `Start PROP-17.1.`

## Operator urgency (read first)

LinearThrone needs Victoria **back up with this behavior today** so bugs can be worked out **today**, and she can **work tomorrow without the hammer/reconnect loops**. Diligent implementation — no prompt-only shortcuts — but **hurried pace**. Land park + rejects first; do not wait on R&R (17.3) or the shell (17.4).

## Problem

`AssignedWorkRunner` keeps looping while `assigned-work.json` is `active`. Failed `desktop_*` / `browser_*` / `guest_*` calls reset the stuck streak, so a dead Maestro/site becomes an endless reconnect. Two task lists diverge (`assigned-work.json` vs `victoria_tasks`). Model-minted `confirmed=true` still consents email/MT4. `POST /settings/tools` lacks the companion token. `guest_shell` allows package install. Host Stop is useless while stuck on “Starting Host…”.

## Root cause (locked)

Hosted loop + dual ledger + trust of model consent. See proposal §3. Prompt rewrite alone is a kill (Avenue B rejected).

## Solution (do this)

### A. One ledger

1. Extend `victoria_tasks` (migration): statuses `todo | in_progress | done | blocked | parked | suspended`; columns for `surface` (`maestro|browser|vm|local`), `park_reason`, `park_until` (unix ms or ISO), identical-failure counter, optional notes.
2. Update `IVictoriaTaskStore` / repo / `task_*` tools to allow the new statuses. **No delete API.**
3. Shrink `assigned-work.json` to `{ "runningId": <long|null> }` only (migrate existing active slot → row + pointer). Runner reads the row, not a second status machine.
4. Runner resumes **only** the running id when status is `in_progress`. Never schedules `parked` or `suspended`.

### B. Park / probe / surface

1. Identical surface failures (same URL / same Maestro unreachable / VM off / browser health fail) increment a counter. **A tool name in the transcript is not progress.**
2. After a small fixed threshold (default **3** identical failures): set `parked`, one-line `park_reason`, `park_until = now + 45m` (PM lock). Clear running pointer.
3. After cooldown: **one** probe. Still down → park again with **120m** cooldown. No reconnect loop.
4. Released work = status `todo` (or operator release from `suspended` → `todo`). Start only when `surface` health is up. Suspended rows are visible and never started.
5. Operator (API/tools LinearThrone uses): **add** (default `suspended` unless marked ready), **suspend** running (stop, keep notes), **release** → `todo`. **No delete.**
6. Done is a real status — drop “both lessons” regex done-detection for assignment completion.

### C. Host rejects (code gates)

1. Strip model-minted `confirmed` on email send/delete and MT4 write paths before consent checks.
2. `POST /settings/tools` requires the same companion token email settings already use.
3. `guest_shell`: refuse package install and pipe-to-bash in **every** mode (`apt`, `snap`, `flatpak`, `wget|bash` / curl|bash). Compile/test under `/home/victoria` stays.

### D. Host Stop unstick

1. Host start via `start-soulcore.ps1` (or current LocalStack start path): timeout; always clear “Starting Host…” UI busy flag.
2. **Stop stays enabled during start** and force-kills the Host process when stuck. (Tray/hotkey for frozen UI thread is out of scope — sibling.)

## Do not

- Implement R&R / `LeisureModel` (PROP-17.3)
- Tab/browser second-instance SOP store (PROP-17.2) — coordinate only if you touch shared browser helpers
- Presence shell redesign (PROP-17.4)
- Prompt-only park or denylist
- Auto-start `victoria-sandbox` to keep her working
- A third task queue

## Acceptance

- [ ] `victoria_tasks` supports `parked` + `suspended` + surface + park fields; unit tests cover store
- [ ] `assigned-work.json` is running-id pointer only; runner does not loop a dead `active` assignment
- [ ] Three identical surface failures → `parked` + reason; no reconnect loop; one probe after 45m then 120m
- [ ] Failed tool call does **not** reset progress / stuck counter as success
- [ ] Suspended never started; release → `todo`; add defaults to `suspended` unless ready; no delete
- [ ] `confirmed` stripped; tools-settings companion token enforced; package-install / pipe-to-bash refused
- [ ] Host Stop force-kills during stuck start; “Starting Host…” clears on timeout
- [ ] Report: `docs/agents/reports/PROP-17.1-BED01-to-PM01.md` with Pass/Fail + evidence

## Handoff

File the report same day if possible. On Pass, PM unblocks **PROP-17.3**. Coordinate with FED if activity-band needs status strings early.
