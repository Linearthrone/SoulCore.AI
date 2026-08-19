---
type: proposal
status: sent-to-pm
tt_id: TT-01
created: 2026-08-19
updated: 2026-08-19
title: "[TINA-main] Victoria dedicated Playwright browser + live stream"
need: Reliable background computer/browser so Victoria can log in and work in seconds; Kurt sees a stream of her browser; web first then VS IDE
sent_at: 2026-08-19
prop_id: PROP-1
pm_intake: docs/agents/tasks/PROP-1-TT01-to-PM01.md
environment: TINA-main
---

# Victoria reliable workspace — DOM browser + observer pane

## 1. Need / Want

Victoria cannot reliably operate programs she needs. Guest Firefox in VirtualBox can open and navigate, then hangs on in-page work (click Login) because the primary control path is screenshot → vision → pixel click, plus flaky AT-SPI and Guest Additions timeouts. She also reports multi-step jobs as done after one or two successful tools.

Kurt wants:

- A workspace she drives **in the background** (not his active Windows window / mouse).
- He **pulls up her view** when she asks what to do next or when he checks progress.
- Login and similar labeled UI in **seconds**, not ~20 minutes.
- Open to: better VirtualBox control, a Windows-session browser she owns in the background, something in between, or new.

## 2. Goal & Success Criteria

- **Login path (labeled UI):** navigate → find Login by name/role → fill fields → submit, typically **under ~30s** of tool time (excluding Kurt 2FA/password).
- **No primary vision loop** for labeled web controls.
- **Honest completion:** she does not say "done" until a **page postcondition** (URL/title/locator), not `Success` on Firefox launch.
- **Kurt's desktop stays his:** her mouse/keyboard never steal his OS pointer; her browser is not his daily Chrome profile.
- **Observer:** ChatDesktop shows a **live (or near-live) stream of Victoria's browser**, plus URL/title — not Kurt's Chrome, not a required-on-top VirtualBox window.
- **Passwords:** accounts are **hers**. She may hold and use those credentials in **her** profile. They must not leak into Host logs, chat traces, or a persisted Presence gallery. Kurt's accounts stay out.

## 3. Context & Constraints

Verified in tree (2026-08-19):

| Layer | Today |
| --- | --- |
| Scope | `DesktopTargetWindowTitle` hard-scopes CUA to Ubuntu VM `victoria-sandbox`. Host `desktop_open_app` blocked. |
| Browser | `GuestVmBrowserBridge` — guest Firefox via Guest Additions. Host Chrome extension `:17891` blocked in VM scope. |
| In-page | `GuestBrowserScript` AT-SPI walk (~450 nodes). Fail → `desktop_screenshot` + pixel `desktop_click`. |
| Guidance | `ComputerUseGuidance` still **screenshot-first for Login**. Unscoped block says "Kurt's Windows desktop"; scoped block contradicts. |
| Navigate success | `browser_navigate` can return Success after **launching Firefox**, not after load/login. |
| Observer | Presence **What she saw** = last PNG + `LastAction`, not live URL/title/failure. Pop-out exists. |
| Isolation intent | Protect Kurt's real Chrome, mouse, files. Guest creds in `SoulCore/.env`. Shared folders off. |
| History | Hermes `computer_use` / `agent-browser` already timed out (ISSUE-20260727-008). Do not revive pixel CUA as the web actuator. |

Thinktank seats: STRAT, CONTRA, SYS, RISK, USER (2026-08-19).

## 4. Clarifying Q&A (answered)

Intake:

- Guest can open a browser and reach a page; hang-up is **in-page** (Login via capture).
- Kurt wants to **see her work** when she asks / he checks; she must not own his foreground session.
- He is **open** to a Windows background browser she drives, or a new design.

Follow-up 2026-08-19:

| Q | A |
| --- | --- |
| Identity / cookies | **Hers.** Dedicated profile is fine (Host or guest). Not Kurt's Chrome. |
| Accounts | **Real accounts, but Victoria's.** She may have the passwords. Fine for this product. |
| Task mix | **Websites first.** Next native surface: **VS IDE**. |
| Observer | **Yes — a stream of the browser she is using.** |

Still open — see §9 (VS flavor, 2FA, AFK).

## 5. Avenues Explored

### Avenue A — Persistent Playwright/CDP (recommended actuator)

Replace screenshot-as-locator with a **long-lived browser protocol** (Playwright `getByRole` / accessibility snapshot / `waitUntil` load). **Do not** invoke Playwright via a fresh `guestcontrol run` on every click (CONTRA: that keeps the hang).

Placement **locked to A1** (user: her cookies on a profile that is not Kurt's Chrome; websites first; he wants a stream of *her* browser). A2 stays a parked isolation upgrade.

| | **A1 Host dedicated Chromium (chosen)** | **A2 Guest Chromium + NAT CDP (parked)** |
| --- | --- | --- |
| What | Playwright in Host against Victoria-only `user-data-dir`. Not Kurt's Chrome. CDP screencast = his stream. | Chrome in VM `--remote-debugging-port`, VBox NAT forward, Host Playwright `connectOverCDP`. |
| Speed | Best (SYS: ~3–5 days MVP). No Guest Additions on the hot path. | ~1–2 weeks. |
| Isolation | Her cookies on Host, **not** Kurt's profile. Hard-forbid host CUA / daily Chrome. | Cookies stay in VM. |

**Do not** attach to **Kurt's daily Chrome** (Avenue C below). That is a kill-route.

### Avenue B — Native apps later (VS IDE is next, not MVP)

Web path stays Playwright. **Visual Studio / VS Code is a second actuator.**

| VS flavor | How she works | Same stream as the browser? |
| --- | --- | --- |
| **B1 VS Code in the browser** (`vscode.dev`, github.dev, or `code-server`) | Still Playwright. Same Chromium, same CDP stream. | **Yes** — preferred if "VS" can mean VS Code in a tab. |
| **B2 VS Code / Visual Studio as a Windows window** | Pixel CUA or IDE APIs (DTE, VS Code CLI/extension). Separate observer (window capture), easy to steal Kurt's focus. | **No** — second pane. |
| **B3 VS Code in the Ubuntu VM** | Guest window + optional noVNC. Isolation good; Guest Additions pain returns. | VM stream, not the browser stream. |

Do **not** use screenshot CUA for web Login. Do **not** put Visual Studio into the Playwright MVP.

### Avenue C — Drive Kurt's real Windows Chrome (rejected)

Attach/debug his running Chrome, background clicks, reuse his cookies.

**Kill-route (CONTRA + RISK):** his banking/session, focus steal, WebAuthn, prompt injection on *his* logged-in sites, contradicts `GuestVmBrowserBridge`. Background is a lie once a picker/2FA appears.

### Avenue D — Patch AT-SPI + screenshot CUA (rejected as primary)

Raise node cap, persist `/tmp/hv-browser.py`, stop Success-on-spawn.

**2–4 days** of work, SPAs/shadow DOM still fail, vision fallback remains. Useful only as **degraded** notes, not the login product.

### Avenue E — Cloud computer-use APIs (rejected)

Off-loopback, often still screenshot-shaped, vendor sees the session, worse fit with SEC-004 and VM snapshots.

### Avenue F — Observer pane (orthogonal; ship with A)

Actuator reliability ≠ watch-her-work.

**Required for this proposal (user asked for a stream):** Playwright **CDP screencast** (or ≥~2–5 fps JPEG pump) → ChatDesktop **Show Victoria's browser**. Overlay: **URL · title · last failed tool · Waiting on you**.

This is **her** Chromium, not Kurt's session, not "last PNG if she happened to screenshot." Last-frame Presence today is **not** that stream; MVP must replace it for the Playwright browser.

Later (optional): VirtualBox VRDE / noVNC for **whole guest desktop** / native VS. Do not block login or the browser stream on this.

## 6. Recommended Route

**Split the workspace. Fix the web actuator. Make "done" mean a postcondition. Give Kurt a PiP of *her* browser.**

1. **Stop now (guidance + tool semantics, before new deps)**  
   - Do not use screenshot as **primary locator** for labeled web UI.  
   - Do not treat AT-SPI-fail + PNG as `browser_snapshot` success.  
   - `browser_navigate` Success only after URL/load wait — not Firefox spawn.  
   - Split `action_ok` vs `goal_complete`. Chat "done" requires postcondition.  
   - Align preamble: scoped VM vs "Kurt's Windows" contradiction.

2. **MVP web path: Avenue A1 (locked)**  
   - New `PlaywrightBrowserBridge : IBrowserBridge` (Host Microsoft.Playwright + Victoria `user-data-dir`).  
   - Tools: navigate, click-by-role/name, fill, a11y snapshot. She may `fill` **her** passwords; redact values in logs/traces.  
   - **Stream:** CDP screencast into ChatDesktop (FED) as the default observer — this is the "I will have a stream" deliverable.  
   - Headed Chromium may exist for debugging; Kurt should not need it. His view is the PiP/stream.  
   - VBox / `desktop_*` **out of the web hot path**. Keep VM for later native work if B2/B3.

3. **Phase 2 — VS IDE:** prefer **B1** (VS Code in her streamed browser) so one actuator + one stream. If he means **Windows Visual Studio**, that is a **new** thinktank/ticket after the browser path works — do not block MVP on DTE/window CUA.

4. **Do not** revive Hermes pixel `computer_use` as the web path. **Do not** attach to Kurt's Chrome.

## 7. Alternatives (parked)

- A2 guest CDP if we later want cookies **only** in Ubuntu.
- B2/B3 native Visual Studio / VM VS Code after web MVP.
- noVNC / VRDE whole-guest live view.
- Avenue D as degraded AT-SPI only.
- Kurt-in-the-loop fill (not required for *her* accounts; keep for *his* accounts if they ever appear).

## 8. Risks & Kill Criteria

**Must-mitigate**

- Dedicated profile ≠ Kurt's Chrome cookies/passwords. No shared folders of his home. No guest↔host clipboard for "easier login."
- Prompt injection: a11y names and page text are untrusted; URL allowlist / confirm-navigate for new origins; refuse OS/payment dialogs in software.
- Presence **stream** is live/near-live in memory. Do not persist password/OTP frames to a gallery; **redact** `fill` values in tool traces even though she is allowed to type her own passwords.
- Host A1: any click that hits a **non-profile** HWND is a stop-ship.
- Guest A2: CDP port only via NAT to Host loopback, not guest-LAN expose.
- Live view stays loopback (SEC-004).

**Kill criteria**

- She can type/click in Kurt's daily Chrome, password manager, or unscoped Explorer.
- Secrets in tool args, Host logs, chat, or Presence gallery.
- Playwright still invoked through Guest Additions **per click**.
- Login still implemented as screenshot-find-button "until Playwright is ready."
- Unattended open-internet control with no injection hardening.
- "Reliable" depends on Guest Additions with no isolated fallback **inside** the guest framebuffer (never unscoped host CUA).

**Seat dissent (do not hide)**

- STRAT: lead with **guest** DOM (A2-like) to keep isolation; park host Chrome.
- SYS: lead with **host** Playwright (A1) because guestcontrol is the bottleneck.
- CONTRA: A2 only if CDP is a **persistent** guest service; A1 is industry-reliable and therefore isolation-dangerous.
- Facilitator + user 2026-08-19: **A1 locked.** Her cookies/passwords on a dedicated Host Chromium. Stream of that browser is in-scope. VS IDE is phase 2.

## 9. Open Questions for User / PM

Answered: identity (hers, dedicated profile OK), accounts (hers, she may use passwords), mix (web first, then VS), observer (stream of her browser).

Still open for PM (do not block browser MVP):

1. **VS flavor:** VS Code **in her streamed browser** (B1) vs **Windows Visual Studio** (B2) vs **VS Code in the Ubuntu VM** (B3)?
2. **2FA:** If her sites require OTP, does Kurt handle the second factor while she waits?
3. **AFK:** May she drive the browser when Kurt is away?

## 10. Suggested PM Handoff

- **Environment:** **TINA-main** (PM-01 / TINA). TT does not ticket FED/BED.
- **Likely roles:** BED-01 (Playwright bridge + tool result schema + guidance + `fill` redaction), FED-01 (**CDP screencast / Victoria's browser stream** + URL/title/status), SEC-01 (profile isolation from Kurt's Chrome, injection/URL policy, no secret gallery), OPS-01 (Playwright Chromium + `user-data-dir` location), QA-01 (login timing + "done" honesty + Kurt's Chrome untouched + stream shows *her* tab).
- **Suggested split / order:**
  1. BED: stop false Success + screenshot-first guidance (hours).
  2. BED: Playwright `IBrowserBridge` MVP (navigate/click/fill/snapshot).
  3. FED: **live stream pane** (not last-PNG-only).
  4. SEC: her-profile vs his-Chrome; redact fills; stream not persisted for password fields.
  5. QA: timed login; stream evidence; Chrome-not-touched.
  6. Later ticket: VS IDE (B1 vs B2 vs B3).
- **What PM should decide first:** Confirm VS flavor before scheduling IDE work. Do not delay the browser stream on Visual Studio.
