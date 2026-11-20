---
type: task
prop_id: PROP-20.7
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Done
created: 2026-11-17
title: SMS reply = answer + public URL; long findings → research paper
depends_on: PROP-20.5
soft_depends: PROP-20.4
siblings: PROP-20.5, PROP-20.6
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.7 — SMS product pipe (answer + link)

**Activate:** `@Agents/BED-01.md` then execute this ticket.

## Problem

SMS should research and text back her answer plus the **external URL she actually opened**. `SmsOutboundService` still cuts at 480 chars — that cut is the pipe. Long findings go to a research paper for Presence. Texted link is the public source, not an internal paper address. Private journal books do not ride in the SMS preamble.

## Solution

1. Inbound SMS path that used tools: compose outbound body as short answer + public URL(s) within 480-char cut.
2. Persist long findings via existing research paper APIs (not private `thoughts`/`wants`/`dreams`).
3. Attach mind block from 20.4 builder when available (same voice as desk).
4. Do not send images/documents as MMS (separate ticket; out of scope).
5. House-changing tools remain denied via 20.5 gate — do not reimplement the deny list; consume it.

## Files to change

- `SoulCore/SoulCore.Host/Companion/SmsOutboundService.cs`
- SMS inbound reply shaping in Companion (avoid fighting 20.5 gate module — extend, don't fork)
- Tests for outbound truncation + URL inclusion

## Do not touch

- Rewriting the entire security test suite (20.5 owns)
- Journal schema, desk Identity UI

## Acceptance

- [x] Outbound SMS ≤480 chars includes answer + public source URL when research opened a page
- [x] Long body landed in research paper store, not private journal books
- [x] Evidence + report: `docs/agents/reports/PROP-20.7-BED01-to-PM01.md`

## Note

If 20.5 has not landed yet, implement against the allow-list contract in the plan and re-run tests after 20.5 merges.
