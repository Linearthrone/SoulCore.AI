---
type: proposal-intake
prop_id: PROP-1
from: TT-01
to: PM-01
priority: P1
status: Intake — TINA-main ticketing
created: 2026-08-19
environment: TINA-main
mode: idea
title: "[TINA-main] Victoria dedicated Playwright browser + live stream"
proposal: docs/agents/unexecuted_proposals/victoria-reliable-workspace-browser.md
assignee_role: PM-01 (TINA)
---

# PROP-1 : [TINA-main] Victoria dedicated Playwright browser + live stream

**For:** **TINA-main** PM-01 environment (House Victoria / TINA).  
**From:** TT-01 thinktank. **Mode:** `idea` (not unblock).  
**Proposal:** `docs/agents/unexecuted_proposals/victoria-reliable-workspace-browser.md`

TT-01 does not ticket FED/BED/OPS/QA. PM-01 owns execution split.

## One-paragraph recommended route

Stop using VirtualBox screenshot/AT-SPI as the **web Login** actuator. Give Victoria a **Host Playwright Chromium** with her own `user-data-dir` (not Kurt's Chrome). She navigates with locators (`click("Log in")`, `fill`), may use **her** real-account passwords, and must not claim done until a **page postcondition**. Kurt gets a **live/near-live CDP screencast** of **that** browser in ChatDesktop — not last-PNG-only Presence, not his daily Chrome, not a required-on-top VM window. VirtualBox stays out of the web hot path. **VS IDE is phase 2** (prefer VS Code in the same streamed browser if that counts as "VS").

## Constraints Kurt locked

- Real accounts, **hers**; she may hold/use those passwords.
- Cookies **hers** on a dedicated profile.
- Work mix: **websites first**; next is **VS IDE**.
- Observer: **stream of the browser she is using**.

## Open questions (do not block browser MVP)

1. VS flavor: VS Code in her browser vs Windows Visual Studio vs VS Code in the Ubuntu VM?
2. 2FA: Kurt handles OTP while she waits?
3. AFK: may she browse when Kurt is away?

## Suggested next tickets (not binding)

| Order | Role | One-line |
| --- | --- | --- |
| 1 | BED-01 | Stop false `Success` on Firefox spawn; screenshot-first Login guidance off |
| 2 | BED-01 | `PlaywrightBrowserBridge` + Victoria profile: navigate / click-by-role / fill / a11y snapshot |
| 3 | FED-01 | ChatDesktop **Show Victoria's browser** CDP stream + URL/title/last-fail |
| 4 | SEC-01 | Isolate from Kurt's Chrome; redact fills in logs; no password frames in gallery |
| 5 | OPS-01 | Playwright Chromium + `user-data-dir` location |
| 6 | QA-01 | Timed login; stream is her tab; Kurt's Chrome untouched |
| later | PM | VS IDE after flavor decision |

## Kill criteria (PM must keep)

- No attach to Kurt's daily Chrome.
- No Playwright-via-guestcontrol-per-click.
- No "logged in" from screenshot success.
- Stream stays loopback (SEC-004).
