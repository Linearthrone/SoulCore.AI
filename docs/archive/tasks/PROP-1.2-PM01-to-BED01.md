---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.2
legacy_task_id: TASK-202
from: PM-01
to: BED-01
priority: P0
status: Pass
created: 2026-08-19
completed: 2026-08-21
wave: 31
phase: phone-digits
title: Host inbound SMS/MMS adapter → One Thread conversationId
depends_on: PROP-1.1
unblocked: 2026-08-21 — PROP-1.1 Pass (SM-X218U tablet MDN)
gateway_device: Samsung Galaxy Tab SM-X218U (native talk/text; not DIGITS)
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
intake: docs/agents/tasks/PROP-1.0-PM01-to-TT01.md
report: docs/agents/reports/PROP-1.2-BED01-to-PM01.md
handoff: 2026-08-19 — WonderWoman (PM-01)
bed_note: 2026-08-21 — POST /api/companion/v1/messages/inbound shipped; next PROP-1.3 outbound
---

# PROP-1.2: Inbound SMS/MMS → Host chat pipeline

## Problem

SoulCore has no carrier SMS. Phone chat must enter the **same** Host conversation as ChatDesktop (`conversationId` / memory), not a second brain.

## Gateway facts (from PROP-1.1 Pass)

- Device: **SM-X218U** always-on tablet on Tailscale.
- Victoria’s number = **tablet cellular MDN** (config/env later — never git).
- Kurt texts that MDN; gateway will POST into Host (this ticket).

## Solution

1. Loopback (or Tailscale-authenticated) HTTP ingest for gateway POSTs (text + optional image bytes). SEC-004: Host bind stays loopback; gateway reaches via Tailscale serve **or** Host-side listener that only accepts Tailscale peer + token — **no Funnel**.
2. Map inbound → existing chat / companion send path so replies use the **shared** conversation with Presence.
3. Kurt allowlist E.164 (config/env, never commit). Unknown inbound = silent drop.
4. Inbound MMS image → thread attachment (not executable / not tool args).
5. Auth: companion/API token on gateway POSTs (reuse or extend `SOULCORE_COMPANION_API_TOKEN` pattern).
6. Document a minimal Android-side poster (Termux script / small companion) that can run on the SM-X218U — BED owns Host contract; OPS can wire the tablet client after API exists.

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
