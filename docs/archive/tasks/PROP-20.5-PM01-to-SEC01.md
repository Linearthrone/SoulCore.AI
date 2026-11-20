---
type: task
prop_id: PROP-20.5
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: SEC-01
priority: P0
status: Pending
created: 2026-11-17
title: SMS tool allow/deny gate + rewrite SmsSecurityGateTests
depends_on: none
siblings: PROP-20.4, PROP-20.7
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.5 — SMS security gate (allow list)

**Activate:** `@Agents/SEC-01.md` then execute this ticket.

## Problem

Current SMS path forces complete-only / no tool loop (`SmsSecurityGateTests` asserts `inboundUsesToolLoop:false`). Decision: SMS may run a **filtered** tool loop. Hostile text must still never call denied tools.

## Allow list (only these)

- `browser_navigate`, `browser_snapshot`, `browser_health`, `browser_tabs`, `browser_back`, `browser_scroll`, `browser_click_text`
- `desktop_screenshot`
- `memory_recall`, `memory_search`
- `research_subject_list`, `research_subject_open`, `research_paper_list`, `research_paper_get`, `research_paper_write`

## Deny list (even if asked)

- `desktop_open_app`, `desktop_click`, `desktop_type`, `desktop_key`, `desktop_drag`
- `browser_fill`, `browser_type`, `browser_key`
- `guest_shell` and any guest write
- `email_send`, `email_draft`, `email_delete`
- `mt4_order`, `mt4_close`, `mt4_modify`
- `workflow_execute`
- `task_release`, `task_delete`, `task_update_status`

## Solution

1. Central SMS tool allow/deny helper; fail closed for unknown tools.
2. Wire inbound SMS tool loop through that filter (security ownership of the gate).
3. Rewrite `SmsSecurityGateTests.cs` for the new contract: allowlisted tools may run; denied tools never run on hostile prompts; MMS bytes still not tool arguments; secrets still redacted; phone allowlist still enforced.
4. Update health snapshot flags if they lied about `inboundUsesToolLoop`.

## Files to change

- `SoulCore/SoulCore.Host/Companion/` SMS inbound + any shared gate helper
- `SoulCore/SoulCore.Protocol.Tests/SmsSecurityGateTests.cs`
- Inference tool registry filter helpers if required for SMS only

## Do not touch

- Mind-block text composition (20.4)
- Outbound “answer + public URL” product shaping (20.7) beyond what the gate requires
- Desk UI, journal schema

## Acceptance

- [ ] Denied tool never executes from SMS inbound (test evidence)
- [ ] At least one allowlisted tool path is permitted by the gate (unit-level)
- [ ] MMS bytes not passed as tool args
- [ ] Report: `docs/agents/reports/PROP-20.5-SEC01-to-PM01.md`
