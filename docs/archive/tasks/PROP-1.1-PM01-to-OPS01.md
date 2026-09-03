---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.1
legacy_task_id: TASK-201
from: PM-01
to: OPS-01
priority: P0
status: Pass
created: 2026-08-19
completed: 2026-08-21
wave: 31
phase: phone-digits
title: Gateway Android — Tailscale kill-test (SMS then MMS)
depends_on: none
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
intake: docs/agents/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.1-OPS01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
pm_note: 2026-08-21 — Kurt Pass on SM-X218U tablet native MDN (DIGITS identity dropped for this ship)
---

# PROP-1.1: Gateway kill-test (OPS) — **Pass**

## Problem

Prove a device we control can terminate SMS/MMS before Host adapters ship. Original brief assumed DIGITS; **PM lock 2026-08-21:** use the gateway tablet’s own talk/text MDN instead.

## Solution (as executed)

1. Spare always-on **Samsung Galaxy Tab SM-X218U** with native talk/text.
2. **Tailscale** on Kurt’s tailnet (no Funnel / no public `:7700`).
3. Kill-test: inbound SMS, outbound SMS, outbound MMS image — **Pass** (Kurt).

## Do not (still)

- Port to Twilio unless later Avenue B Host path fails.
- Bind Host non-loopback / enable Funnel.
- Commit MDN / tokens to git.

## Acceptance

| # | Criterion | Status |
| --- | --- | --- |
| 1 | SMS inbound + outbound Pass on gateway MDN | **Pass** |
| 2 | At least one MMS image outbound Pass | **Pass** |
| 3 | Tailscale path only; no Funnel | **Pass** |
| 4 | Report with evidence (redact MDN / tokens) | **Pass** → `PROP-1.1-OPS01-to-PM01.md` |

## Reply

`docs/agents/reports/PROP-1.1-OPS01-to-PM01.md`
