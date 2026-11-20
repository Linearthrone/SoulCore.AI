---
type: task
prop_id: PROP-20.1b
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: OPS-01
priority: P0
status: Pending
created: 2026-11-17
title: Deploy Host + Presence with PROP-20.1 Identity client
depends_on: PROP-20.1
upstream: docs/agents/reports/PROP-20.1-PM01-accept.md
siblings: PROP-20.3b
---

# PROP-20.1b — OPS Identity desk deploy

**Activate:** `@Agents/OPS-01.md`

## Change

FED landed Identity tab → `GET /settings/identity` (`SoulCoreIdentitySettingsClient` + formatter). Needs running Host + rebuilt ChatDesktop.

## Action

1. `dotnet build` Host Release — must succeed (dated-recall APIs should already be on `IMemoryStore` / `SqliteMemoryStore`; if build fails, stop and report compile errors — do not hack FED).
2. Restart Host (home-pc / ALLSTART per runbook).
3. Rebuild/restart ChatDesktop Presence with 20.1 bits.
4. Probe: `GET http://127.0.0.1:<port>/settings/identity` returns JSON with `anchors` / `identityAnchors`.
5. Note port actually used (7700 vs 7710).

## Acceptance

- [ ] Host + Presence running on this wave’s build
- [ ] `/settings/identity` 200 with real or empty anchors (not connection refused)
- [ ] Report: `docs/agents/reports/PROP-20.1b-OPS01-to-PM01.md` with command output

## Note

May overlap PROP-20.3b Host restart — one rebuild can satisfy both if same tree; still file this report covering Identity probe.
