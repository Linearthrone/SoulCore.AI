---
type: task
prop_id: PROP-20.1
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: FED-01
priority: P0
status: Pending
created: 2026-11-17
title: Identity tab shows live GET /settings/identity anchors
depends_on: none
siblings: PROP-20.2, PROP-20.3, PROP-20.4, PROP-20.5, PROP-20.6
proposal: docs/agents/unexecuted_proposals/victoria-mind-path.md
---

# PROP-20.1 — Identity honesty (desk)

**Activate:** `@Agents/FED-01.md` then execute this ticket.

## Problem

`UpdateIdentityDetail` in `House/House.ChatDesktop/MainWindow.axaml.cs` paints charter from `/health` lock fraction and static placeholder text. Host already returns live anchors on `GET /settings/identity` (`identityAnchors` / `anchors` with title + body).

## Solution

1. Fetch `GET /settings/identity` (same Host base as other companion settings).
2. Render each returned anchor title + body in the Identity detail UI (prefer `anchors` or show identity + others clearly).
3. Stop treating `/health` lock fraction as the only identity content.
4. Keep display name wiring; do not invent biography text if the API is empty — show empty/honest state.

## Files to change

- `House/House.ChatDesktop/MainWindow.axaml.cs` (`UpdateIdentityDetail` and call sites)
- Avalonia XAML only if needed for list/scroll of anchors
- Optional thin HTTP helper under ChatDesktop — **not** Host/Memory

## Do not touch

- Backend charter schema, ChatContextBuilder, SMS, journal books
- Sibling Wave A tickets' paths

## Acceptance

- [ ] Desk Identity lists live Host anchor titles + bodies from `/settings/identity`
- [ ] Empty/error Host response does not fabricate anchors
- [ ] Local build evidence for ChatDesktop
- [ ] Report: `docs/agents/reports/PROP-20.1-FED01-to-PM01.md` with request/response or UI evidence
