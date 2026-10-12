---
type: intake
prop_id: PROP-17-victoria-between-talks
from: TT-01
to: PM-01
mode: idea
environment: TINA-main
status: Accepted — PM ticketed PROP-17.1–17.5
priority: P1
created: 2026-10-12
updated: 2026-10-12
title: "[TINA-main] Victoria between talks — park, R&R, Presence shell"
assignee_role: PM-01 (TINA)
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
pm_accept: docs/agents/reports/PROP-17-PM01-accept.md
pm_tickets:
  - docs/agents/tasks/PROP-17.1-PM01-to-BED01.md
  - docs/agents/tasks/PROP-17.2-PM01-to-BED01.md
  - docs/agents/tasks/PROP-17.3-PM01-to-BED01.md
  - docs/agents/tasks/PROP-17.4-PM01-to-FED01.md
  - docs/agents/tasks/PROP-17.5-PM01-to-FED01.md
---

# PROP-17 : [TINA-main] Victoria between talks

**For:** **TINA-main** PM-01 (House Victoria / TINA).
**From:** TT-01.
**Mode:** `idea`.

## Proposal

[`docs/agents/unexecuted_proposals/victoria-between-talks.md`](../unexecuted_proposals/victoria-between-talks.md)

## Summary

Victoria keeps hammering the last assignment when the world is down, because the Host treats that assignment as still active and a failed tool call as progress. She also opens duplicate tabs and has installed a second browser. LinearThrone wants her to keep existing between talks: park a dead surface, do other released work only if that surface is up, and otherwise self-start creative research (notes, papers, pictures, YouTube, a thread such as neural interfaces) on a small local model with no creativity clock.

She must not appoint that research as the work assignment. Buy, email, install, trade, or dropping the lesson to build it stays `suspended` until he releases it. He can add and suspend tasks. There is no delete. Presence settings collapse to three tabs. Host Stop has to kill a start that is stuck on “Starting Host…”.

## Decisions already locked

- No 20-minute cap on an interest. Park and one later probe stop reconnect loops. A living research thread is not on a timer.
- R&R uses `LeisureModel` on local `:11434` only. No cloud fallback. No substitute of the work model. If it cannot load, she stays quiet.
- Creative research is self-starting. Action proposals are not.
- One ledger: `victoria_tasks`. The JSON assignment file becomes a running-id pointer.
- One Thread is not the work log.
- Page text is untrusted data, not the next system prompt.
- Unlimited mailboxes, operator-only. Strip model-minted `confirmed`. Companion token on `POST /settings/tools`.
- Package install refused on `guest_shell` in every mode.
- Phone this round: mood word and doing-line only.
- PROP-16 update/restart behavior moves onto System. It is not dropped.

## Suggested splits

PM may re-split. Do not start 17.3 until 17.1 can park and 17.2 can refuse a second tab and a second browser.

- `PROP-17.1` BED-01 — Ledger, park, suspend, release, surface tag, host rejects (`confirmed`, tools-settings token, package install), Host Stop force-kill.
- `PROP-17.2` BED-01 — Tab and browser invariants, SOP store, life-hacks journal.
- `PROP-17.3` BED-01 — Local R&R, inference lease, self-started research versus held action proposals, transcript stays human.
- `PROP-17.4` FED-01 — Presence shell (three tabs, composer, activity band, VM buttons, email dialog, Identity portrait).
- `PROP-17.5` FED-01 — Companion mood word and doing-line. Android email editor follows the new account model. No phone redesign.

## Open questions

None that block ticketing. PM may name the default small local model and tune cooldown minutes. Missing leisure model means R&R stays quiet.

## What to decide first

Ticket 17.1 before 17.3. Shell layout in 17.4 may start in parallel. Activity-band copy that claims she parked or is researching has to wait until those statuses exist.

## PM disposition (2026-10-12)

**Accepted.** Avenue A. Tickets issued — see `docs/agents/reports/PROP-17-PM01-accept.md`.

PM locks: LeisureModel `qwen2.5:1.5b`; park 45m then probe, re-park 120m; threshold 3 identical failures. Same-day urgency: Victoria park-capable and shell operable today for bug-out; workable tomorrow.
