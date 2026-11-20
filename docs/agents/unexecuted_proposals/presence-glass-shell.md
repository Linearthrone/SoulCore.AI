---
type: proposal
status: shipped-1.1.3
tt_id: TT-01
created: 2026-11-19
updated: 2026-11-19
title: Presence glass shell — S26 Ultra slab, drawers, radial status
need: Replace the wide brushed-metal Presence mess with a portrait glass slab, edge drawers, honest status orb, and operator work/calendar surfaces
prop_id: PROP-22
mockup: docs/agents/unexecuted_proposals/assets/prop22/
pm_tickets:
  - PROP-22.1-PM01-to-DBD01
  - PROP-22.2-PM01-to-BED01
  - PROP-22.3-PM01-to-SEC01
  - PROP-22.4-PM01-to-FED01
  - PROP-22.5-PM01-to-FED01
  - PROP-22.6-PM01-to-FED01
  - PROP-22.7-PM01-to-FED01
  - PROP-22.8-PM01-to-FED01
  - PROP-22.9-PM01-to-FED01
  - PROP-22.10-PM01-to-QA01
  - PROP-22.11-PM01-to-OPSHOME
---

# PROP-22 — Presence glass shell

## 1. Need

Presence is a disorganized wide metal dashboard. Chat mixes system log lines. VM, identity, and settings fight for space. Visual language feels retro-future and dumps data like code. Operator wants a phone-slab glass companion: Galaxy S26 Ultra proportions, dull neon rim, edge drawers, radial status, honest Victoria surfaces, tasks/projects/calendar, and settings that mask secrets.

## 2. Goal & success

- Main window is a 19.5:9 glass slab (default ~460×996), ratio locked, frameless chrome preserved.
- VM lives in a popup drawer (left/right from settings). Radial status projects outside the upper-left corner.
- Chat has bubbles only; stack/system lines go to Performance log.
- Composer grows to 7 lines; round send; call button with full→top-third scrim + sphere.
- Victoria drawer: honest doing-now, last activity→journal, screens for journal/activities/research/bond/tasks/calendar; portrait card with real model profiles + trait bars.
- Settings: sound + updates first; masked token rows; Host/VM controls; system log; advanced ports + mailbox behind confirm.
- No fabricated “doing” copy. Intentionally-off services do not paint yellow/amber/red.

## 3. Field / route contract (locked for parallel seats)

### 3.1 Operator work items (new; does not replace `victoria_tasks`)

Table `operator_work_items` (migration `016`):

| Column | Type | Notes |
| --- | --- | --- |
| id | INTEGER PK | |
| kind | TEXT | `task` \| `project` |
| title | TEXT NOT NULL | |
| description | TEXT | |
| details | TEXT | |
| deadline_utc | TEXT NULL | ISO-8601 |
| priority | INTEGER | 1–5 |
| no_checkin | INTEGER | 0/1 — start–finish without check-in |
| phase_checkpoints_json | TEXT | project only |
| deliverables_json | TEXT | project only |
| needs_extra_permissions | INTEGER | 0/1 |
| outside_collaboration | INTEGER | 0/1 |
| status | TEXT | `open` \| `done` \| `cancelled` |
| created_at | TEXT | |
| updated_at | TEXT | |

Routes (companion auth when token configured):

- `GET /operator/work-items` → `{ count, items: [...] }`
- `POST /operator/work-items` body matches columns (kind required)
- `DELETE /operator/work-items/{id}`

### 3.2 Agenda items (week reminders)

Table `operator_agenda_items` (same migration or `017`):

| Column | Type | Notes |
| --- | --- | --- |
| id | INTEGER PK | |
| title | TEXT NOT NULL | |
| weekday | INTEGER | 0=Mon … 6=Sun |
| time_local | TEXT | `HH:mm` |
| notes | TEXT | |
| created_at | TEXT | |

- `GET /operator/agenda` → `{ items }`
- `POST /operator/agenda`
- `DELETE /operator/agenda/{id}`

### 3.3 Inference profiles

- `GET /settings/inference/profiles` → `{ active: { model, cloud, leisureModel }, profiles: [{ id, label, model, cloud }] }`
  - Profiles = configured Model, LeisureModel, plus Ollama `/api/tags` when reachable.
- `POST /settings/inference/apply` `{ model, cloud? }` — mutates live inference selection for subsequent replies; does not rewrite secrets.

### 3.4 Host log tail

- `GET /settings/system-log?lines=200` → `{ available, path?, lines: string[] }` — loopback companion auth; never invent lines.

### 3.5 Desktop local settings additions

`LocalUiSettings`:

- `VmDrawerSide`: `left` \| `right` (default `right`)
- Window defaults: width 460, height 996; aspect lock 19.5:9 in layout code.

### 3.6 Status priority (orb)

1. Fast red flash — Host down / `/health` unreachable  
2. Solid red — companion WS or active inference path down  
3. Slow pink pulse — non-empty `presence.currentActivity` OR chat/tool turn OR call live (never hides 1–2)  
4. Amber — ≥2 non-critical downs among counted services  
5. Yellow — exactly 1 non-critical down  
6. Green — all counted up  

Intentionally off (`enabled=false`, no mailbox, CUA unchecked) = dim off, not a down.

## 4. Mockups

`docs/agents/unexecuted_proposals/assets/prop22/` — boards 01–06.

## 5. Suggested PM handoff

See frontmatter `pm_tickets`. Parallel: 22.1 + 22.4 + 22.5 + 22.6 + 22.7. 22.2 after 22.1. 22.8/22.9 after contracts. 22.10 then 22.11.
