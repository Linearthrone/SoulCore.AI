---
prop_root: PROP-2-ue-reliable-embodiment
type: task
prop_id: PROP-2.1
legacy_task_id: TASK-210
from: PM-01
to: REX-01
priority: P0
status: Pending
created: 2026-08-19
wave: 31
phase: ue-reliability
title: Shadow PIE sit-down — 1P Kayleigh possess (finish TASK-191)
depends_on: none
proposal: docs/archive/proposals/victoria-ue-reliable-embodiment.md
intake: docs/agents/tasks/PROP-2.0-PM01-to-TT01.md
prior: docs/agents/tasks/TASK-20260817-191-PM01-to-REX01.md
report: docs/agents/reports/PROP-2.1-REX01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
staffing: REX-01 + UE specialist as needed; parallel to Host/DIGITS
---

# PROP-2.1: Shadow Play — Kayleigh 1P possess Pass

## Problem

TASK-191 is **Partial**: C++/P4 on disk but live PIE can still be `DefaultPawn`. Kurt cannot trust the session until Play on **shadow** shows he **is Kayleigh**.

## Solution

1. Author from **this machine’s Perforce** MyProject; **Play/PIE only on shadow PC**.
2. Full module rebuild + editor restart + **save Home** World Settings on shadow.
3. Possessed class must contain **Kayleigh** (`BP_KayleighCharacter` / equivalent) — **not** Victoria, **not** flying DefaultPawn.
4. Strict **first person**; WASD grounded.
5. Evidence: screenshot of possessed class / pawn name + short clip or still of 1P view. Host `success:true` alone = **not Pass**.
6. One body WebSocket `:8888` (kill duplicates).

## Do not

- Possess Victoria as player.
- Reparent `BP_MHC_*`.
- Pass on Live Coding / RC Python / pipeline log alone.
- Block Playwright or DIGITS work.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Shadow PIE screenshot proves Kayleigh possess |
| 2 | Not Victoria / not DefaultPawn / not ghost fly |
| 3 | Strict 1P |
| 4 | Report updates TASK-191 status → Pass (or cites residual) |

## Reply

`docs/agents/reports/PROP-2.1-REX01-to-PM01.md`
