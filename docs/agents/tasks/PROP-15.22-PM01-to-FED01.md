---
type: task
prop_id: PROP-15.22
prop_root: PROP-15-persona-creation-platform
from: PM-01
to: FED-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: none
siblings: PROP-15.21 (OPS Host recycle)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Persona switcher honesty + Host/Presence version currency chrome
---

# PROP-15.22 — Select personas + know if builds are current

## Problems

1. **Persona select:** When Host lacks `/api/personas` (404) or activate fails, switcher stays opaque (“Load personas…” / type names). Need clear status + retry; ComboBox must show **DisplayName** (ItemTemplate / DisplayMemberBinding). After Host recovers, Refresh must reload packs.
2. **Version currency:** Title bar shows `Presence X · Host Y` but not whether either is **latest** or **stale for CreatorEdition**. Update feed default still points at LinearThrone `SoulCore.AI` (wrong fork). Operator needs: current Presence version, current Host version from `/health`, Presence update available (Velopack), and a **Host stale** signal when personas API missing or Host version older than Presence major.minor expectation.

## Solution

1. PersonaSwitcher: ItemTemplate → DisplayName (+ id muted); on list HTTP 404/unreachable show red/orange status “Host has no persona API — restart CreatorEdition Host”; Refresh button already exists — wire title-bar retry if missing.
2. BuildVersionsText / Settings → Updates:
   - Always show Presence version + Host version (from health).
   - Presence: keep Velopack check; show **Up to date** / **Update available: vZ** / **Unpackaged**.
   - Host: if `/api/personas` 404 or health has no `persona` object → **Host outdated (restart from CreatorEdition tree)**.
   - Point default update feed at **House-VictoriAI/SoulCore.AI_CreatorEdition** (or env override), not LinearThrone main.
3. Optional: compare Host `version` to Presence `Version` and show “Host behind Presence” when Host &lt; Presence (informational).

## Do not

- Change Host versioning scheme beyond display (BED bumps Host csproj if OPS finds rebuild still says 0.1.5)
- Stress-suite work

## Acceptance

- [x] Switcher shows pack display names; 404 Host shows actionable status
- [x] Chrome/Settings clearly answer: Presence version, Host version, Presence latest?, Host OK for personas?
- [x] CreatorEdition GitHub feed default
- [x] Report `docs/agents/reports/PROP-15.22-FED01-to-PM01.md`
