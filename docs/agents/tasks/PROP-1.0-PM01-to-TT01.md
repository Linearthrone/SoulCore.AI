---
prop_root: PROP-1-digits-sms-channel
type: task
prop_id: PROP-1.0
legacy_task_id: TASK-200
from: PM-01
to: TT-01
priority: P1
status: Accepted — PM ticketed 201–206
created: 2026-08-19
wave: 31
phase: phone-digits
title: "[WonderWoman] Accept DIGITS SMS/MMS — Avenue B locked"
proposal: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
tt_intake_alias: TT used TASK-194 (collides with Playwright BED-194) → remapped to PROP-1.1..1.6
assignee_role: PM-01 (WonderWoman)
report: docs/agents/reports/PROP-1.0-PM01-to-TT01.md
---

# PROP-1.0: Accept DIGITS SMS/MMS (Avenue B)

**PM:** WonderWoman (PM-01). **From:** TT-01 idea intake.  
**Proposal:** `docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md`

## Decision

**GO — Avenue B locked.** Keep T-Mobile DIGITS MDN. SMS/MMS via Android gateway over Tailscale into SoulCore.Host. Same Host `conversationId` as ChatDesktop. Kurt-only allowlist. MMS screenshot = phone observer. Voice/PSTN **out**. Link Messenger rewrite **parked** (`victoria-link-messenger-product.md`).

## PM defaults (do not block kill-test)

| Open Q | PM default |
| --- | --- |
| Gateway device | **Spare always-on Android** (not DIGITS second-line on daily phone) |
| Host off | **Silence** (no fake “Victoria” away-SMS) |
| Kurt phone OS | Assume **Android Messages** green-bubble until Kurt says iPhone |

## Tickets filed

| ID | To | Focus |
| --- | --- | --- |
| 201 | OPS-01 | DIGITS on spare Android + Tailscale kill-test (SMS then MMS) |
| 202 | BED-01 | Inbound SMS/MMS HTTP → same chat pipeline + conversationId |
| 203 | BED-01 | Outbound SMS/MMS + `send_screenshot_mms` (Playwright/Presence still) |
| 204 | SEC-01 | Allowlist, no tools from inbound, no Funnel, redact |
| 205 | QA-01 | Kurt SMS round-trip + MMS still Pass |
| 206 | FED-01 | Link → status + ComfyUI **only after** 205 Pass |

## Kill criteria (keep)

No Funnel/public Host bind; no stranger auto-reply; no 911; no inbound→tools; no gut Link before SMS/MMS Pass; no Twilio port as first attempt; no RCS scrape.
