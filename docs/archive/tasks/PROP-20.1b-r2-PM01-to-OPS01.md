---
type: task
prop_id: PROP-20.1b-r2
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: OPS-01
priority: P0
status: Accepted
created: 2026-11-17
title: Interim Debug Host+Presence — Identity probe + migration 012
depends_on: PROP-20.1
supersedes: PROP-20.1b
also_covers: PROP-20.3b interim
pm_reject: docs/agents/reports/PROP-20.1b-PM01-reject.md
---

# PROP-20.1b-r2 — OPS Debug interim deploy

**Activate:** `@Agents/OPS-01.md`

## Context

Release is blocked on SixLabors license (PROP-20.1b Fail). PM authorizes **Debug** for Wave A smoke only — not the production Release sign-off.

## Action

1. `.\ALLSTART.ps1 -Configuration Debug -RestartHost` (or equivalent home-pc Debug Host + Presence).
2. Probe `GET /settings/identity` — paste status + JSON shape (`anchors` / `identityAnchors`).
3. Confirm Memory migration `012` present after Host open (sqlite `schema_migrations` or log) — covers PROP-20.3b interim.
4. Record ports actually listening.

## Do not

- Claim Release production deploy
- Patch product code for the license gate (BED owns PROP-20.1c)

## Acceptance

- [ ] Host + Presence up on **Debug**
- [ ] `/settings/identity` reachable
- [ ] Evidence for schema `012` if DB path known
- [ ] Report: `docs/agents/reports/PROP-20.1b-r2-OPS01-to-PM01.md`
