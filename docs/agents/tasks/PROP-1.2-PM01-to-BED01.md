---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.2
legacy_task_id: TASK-202
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-08-19
wave: 31
phase: phone-digits
title: Host inbound SMS/MMS adapter → One Thread conversationId
depends_on: PROP-1.1
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
intake: docs/agents/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.2-BED01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
---

# PROP-1.2: Inbound SMS/MMS → Host chat pipeline

## Problem

SoulCore has no carrier SMS. Phone chat must enter the **same** Host conversation as ChatDesktop (`conversationId` / memory), not a second brain.

## Solution

1. Loopback (or Tailscale-authenticated) HTTP ingest for gateway POSTs (text + optional image bytes). SEC-004: Host bind stays loopback; gateway reaches via Tailscale serve **or** Host-side listener that only accepts Tailscale peer + token — **no Funnel**.
2. Map inbound → existing chat / companion send path so replies use the **shared** conversation with Presence.
3. Kurt allowlist E.164 (config/env, never commit). Unknown inbound = silent drop.
4. Inbound MMS image → thread attachment (not executable / not tool args).
5. Auth: companion/API token on gateway POSTs (reuse or extend `SOULCORE_COMPANION_API_TOKEN` pattern).

## Do not

- Execute tools from inbound SMS/MMS.
- Dual conversation stores (Link Home vs SMS as two memories).
- Public webhook on `:7700`.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Allowlisted SMS appears in same Host thread ChatDesktop sees |
| 2 | Unknown sender dropped |
| 3 | Inbound image stored as media, not tool input |
| 4 | Release 0 warnings; unit/integration coverage for allowlist |

## Reply

`docs/agents/reports/PROP-1.2-BED01-to-PM01.md`
