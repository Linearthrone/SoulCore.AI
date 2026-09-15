---
type: task
prop_id: PROP-4.1
prop_root: PROP-4-presence-shell-honest-hud
from: PM-01
to: FED-01
priority: P1
status: Partial — open (Windows visual QA + unlanded branch slice)
created: 2026-09-05
updated: 2026-09-15
wave: wipeout-now
title: Presence House drawer + honest HUD (match mockup)
depends_on: none
proposal: docs/agents/unexecuted_proposals/presence-shell-honest-hud.md
intake: docs/agents/tasks/PROP-4-TT01-to-PM01.md
mockup: docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png
report: docs/agents/reports/PROP-4.1-FED01-to-PM01.md
---

# PROP-4.1 — House drawer + honesty

## Solution

1. Implement **House drawer** per mockup (not rail): identity strip + chat; drawer for SoulCore/Ollama/Unreal/Comfy/CUA/Sandbox lamps.
2. Pip when SoulCore/Unreal down while closed.
3. Confirm/hold to stop SoulCore.
4. Honest mood/activity (not SoulLoop slogan HUD).
5. Sight = timestamp + folder on **scratch** only.
6. Window icon present.

## Do not

- Installer/Velopack (PROP-4.2 OPS)
- Messenger rewrite (PROP-3)
- Block PROP-1/2/5 on this lane

## Acceptance

Matches mockup closed/open; honesty rules; report with screenshots.

## Status (2026-09-15 registry reconcile)

Ticket was still `Pending` after a **Partial** report landed on `main` via PR #87. Corrected to
`Partial — open`. This ticket **stays active**; it is not archived.

Landed on `main`: drawer/pip/hold-stop, honest mood + activity, sight stamp, window icon
(`House/House.ChatDesktop/MainWindow.Presence.cs`, `MainWindow.axaml`).

Still only on `cursor/prop4-presence-drawer-8a1f` — **do not land in a docs pass**:

- `House/House.ChatDesktop.Tests/PresenceHonestyTests.cs` (honesty unit tests)
- `House/House.ChatDesktop/Properties/AssemblyInfo.cs` (`InternalsVisibleTo`)
- Avalonia `RadialGradientBrush` `Radius` → `RadiusX`/`RadiusY` fix in `MainWindow.Presence.cs`

Remaining to close: land that slice in its own PR, then Windows visual QA on the Presence box.
One stale claim in the report is now false — the mockup
`docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png` **is** on `main`.
