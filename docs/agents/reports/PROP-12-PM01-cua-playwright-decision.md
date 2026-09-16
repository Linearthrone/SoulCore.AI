---
type: pm-decision
from: PM-01 (TINA device-side)
created: 2026-09-16
prop_id: PROP-12
title: CUA vs Playwright — locked working stack
status: decided-and-shipping
---

# PROP-12 — Computer use / Playwright resolution

Kurt asked PM-01 (TINA device-side) to talk to Victoria and end months of thrashing on
computer-use + Playwright. Live evidence 2026-09-16:

## What was actually broken

| Symptom | Root cause | Not the cause |
| --- | --- | --- |
| Web work "doesn't work" / Victoria blames VM | Two actuators conflated. Web = Host Playwright. Desktop = CUA scoped to `victoria-sandbox`. | Missing Chromium (chromium-1148 is installed) |
| Tool loop stalls on open website | ForceTool=`browser_navigate` exclusivity **refused** `browser_health` | Playwright bridge itself |
| Desktop tools fail | `victoria-sandbox` was **powered off** | Playwright |
| Observer empty (`/browser/view` no image) | No successful navigate yet this Host session | Profile path |

## Evidence

- `dotnet test … PlaywrightBrowserBridgeTests.Navigate_ExampleCom` → **Passed** (~4s), JPEG published.
- Host health: `browserBackend=playwright`, `desktopBackend=cua`, `desktopTargetWindowTitle=victoria-sandbox`, `cuaDriverAvailable=true`.
- Host log (pre-fix): `refused non-forced tool 'browser_health' (required=browser_navigate)`.
- Victoria SoulLoop already recalling: unrunning `victoria-sandbox` — correct for **desktop_***, wrong as a web blocker.
- VM started 2026-09-16; guest IP `10.0.2.15`.
- **Live E2E 2026-09-16 14:58:** WS `Open https://example.com` → `browser_navigate ok=True` (~1.1s tool) →
  `GET /browser/view` = `hasImage=true`, `url=https://example.com/`, `title=Example Domain`.
  Victoria reply confirmed the page opened.

## Locked stack (this week — no more avenue shopping)

1. **Web / login / click labeled UI** → Host **Playwright only** (`browser_*`).  
   - Profile: `%LOCALAPPDATA%\SoulCore\victoria-browser`  
   - Observer: Presence **What she saw** / `GET /browser/view`  
   - **VirtualBox is NOT required** for web.
2. **Native desktop inside the Ubuntu sandbox** → **CUA** + `desktop_*`, window title `victoria-sandbox`.  
   - ALLSTART / OPS must keep the VM running when desktop work is expected.
3. **Do not** revive Hermes `computer_use`, guest Firefox AT-SPI as primary web path, or pixel-click Login on web.

## Code shipped in this unblock (PM emergency)

- ForceTool companions: under `browser_navigate`, allow `browser_health` / `browser_snapshot` / `browser_tabs`.
- Deferred Playwright pre-dispatch when the prompt has follow-on actions (keep force + companions).
- Unit test: `ForceToolName_BrowserNavigate_AllowsBootstrapBrowserHealth`.

## Operator how-to (stop thrashing)

- Ask: `Open https://example.com` → expect Presence browser pane to update.  
- Ask desktop-in-VM work only when VirtualBox `victoria-sandbox` is running.  
- If Playwright missing binaries: `.\SoulCore\scripts\install-playwright.ps1` then `.\ALLSTART.ps1 -RestartHost`.

## Follow-ups (not blockers for "web works")

- OPS: auto-start `victoria-sandbox` with ALLSTART when desktop tools enabled (optional).  
- FED: make Presence browser pane fail-loud when `hasImage=false` after a navigate ask.  
- QA: one TC for navigate → `/browser/view` hasImage + URL contains host.
