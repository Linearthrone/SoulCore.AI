---
type: report
prop_id: PROP-17.2
from: BED-01
to: PM-01
status: Pass
created: 2026-10-12
updated: 2026-10-12
---

# PROP-17.2 — Browser hygiene, SOP store, life-hacks — Pass

## Done

1. One live tab per URL: Playwright `NavigateAsync` reuses/focuses an existing page; guest Firefox tabs reuse before spawn.
2. Second browser refused when one is live (`DesktopOpenAppTool` + guest `OpenAppAsync`).
3. Browser install refused via `guest_shell` package gate + open_app install intent check; site-down returns `failure_code=surface_browser_down` for 17.1 park.
4. `victoria_sops` table + two seed SOPs (browser hygiene; site down means park). Authority comment: tool code → charter → settings → notes. SOP tools are recall-only (cannot flip gates / mint confirmed / call guest_shell).
5. Journal migration 010: `life_hacks` fourth book; CHECK extended; store allow-list updated.

## Evidence

- `Prop17ParkAndSopTests` (SOP seeds, guest_shell refuse, URL normalize, park cooldowns)
- `SqliteMemoryStoreJournalTests` (four books including life_hacks)
- `GuestWorkspaceTests` + Prop17 package/pipe-bash cases
- Full targeted filter: 113 passed with 17.1 suite

## Files

- `BrowserHygiene.cs`, Playwright/guest navigate + `DesktopOpenAppTool`
- Migrations `009_victoria_sops.sql`, `010_life_hacks_journal.sql`
- `IVictoriaSopStore` / repo / `sop_*` tools
- Journal repository allow-list + tests
