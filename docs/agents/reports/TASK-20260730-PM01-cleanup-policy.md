---
type: note
from: PM-01
id: TINA
created: 2026-07-30
title: Multi-stage cleanup policy
---

# Codebase cleanup policy (TINA)

## Stages

| Stage | Scope | Status |
| --- | --- | --- |
| **1 — Hygiene** | Untrack `artifacts/`, script `*.log`/`*.pid`, Hermes venv leftovers, ephemeral QA dumps; expand `.gitignore` | This PR |
| **2 — Agent queue** | Move Closed/Fixed issues → `docs/agents/issues/closed/`; keep `log/` as audit trail (small); reports/ = active only | This PR |
| **3 — Dead / dup code** | SLOP-01 scan of SoulCore + House for unused types, duplicate helpers, abandoned scripts | Ticketed after merge |

## Keep

- `docs/agents/log/` — completed tickets (audit). ~1.7 MB; do **not** delete without a dated archive export.
- Reusable `SoulCore/scripts/*.ps1` and `e2e/` / `qa-*/` **scripts** (not their `.log` output).
- Open issues under `docs/agents/issues/`.
- `unexecuted_proposals/` until withdrawn.

## Do not commit

- Host publish trees, SQLite DBs, process pid/log files, Android/UI dump folders under `reports/_qa*_evidence/`.
