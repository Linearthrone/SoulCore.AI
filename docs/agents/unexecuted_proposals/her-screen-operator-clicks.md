---
type: proposal
status: unexecuted
tt_id: TT-01
prop_id: PROP-19-her-screen-operator-clicks
created: 2026-10-19
updated: 2026-10-19
title: Her screen — Kurt intervene clicks dead; Victoria clicks land but off soft-cursor
need: Operator clicks on the embedded VM pane do not reach the guest at all; Victoria’s tool clicks do land but miss where the shown mouse/soft-cursor aims — workable but wrong
related:
  - docs/agents/issues/ISSUE-20261014-001-her-screen-wheel.md
  - docs/agents/reports/PROP-12-vm-embed-primary-amendment.md
  - docs/superpowers/specs/2026-10-07-her-screen-vm-hover-coords-design.md
  - docs/agents/unexecuted_proposals/victoria-browser-live-cursor-pane.md
sent_at:
pm_intake:
---

# Her screen operator clicks + Victoria aim offset

## 1. Need / Want

Kurt reports:

1. **His** clicks on Her screen (embedded `victoria-sandbox`) **do not get through at all**.
2. **Victoria’s** clicks **do land**, but are **off target** from where the mouse / soft-cursor shows — good enough to limp, wrong for trust / intervene.

He needs intervene / assist / verify. Soft-cursor for her automation must match the real hit.

## 2. Why this happens (honest diagnosis)

Thinktank + code inventory (2026-10-19): **two failure modes on one stack**, not one shared “mouse bug.”

### Architecture (shipping)

Victoria and Kurt do **not** share “click the VirtualBox HWND and the guest sees it.”

| Actor | Path |
| --- | --- |
| **Victoria** | Tool → soft-cursor Presence feedback → `VboxConsoleMouse` / `putMouseEventAbsolute` (COM) — or Playwright `page.Mouse` on browser path |
| **Kurt** | Nearly invisible full-pane `HerScreenInput` overlay (`HerScreenHwndOverlay`) sits **on top** of the SetParent’d VBox HWND → `WM_LBUTTON*` → `VictoriaVmPointer` → **same COM Absolute** inject (+ host cursor park for Mouse Integration) |

Reparented VirtualBox **paints** in the pane but **does not forward Win32 input into Ubuntu**. If Kurt’s click only hits the VBox surface (overlay missing / wrong z-order), the guest is deaf by design.

Key files: `HerScreenHwndOverlay.cs`, `VictoriaVmPointer.cs`, `VictoriaVmPointerPlan.cs`, `VictoriaBrowserCoordMap.cs`, `VictoriaBrowserEmbedHost.cs`, `VboxConsoleMouse.cs`, `HostPointerPark.cs`, `SoftCursorPresenceFeedback.cs`.

Related open issue: `ISSUE-20261014-001` (wheel / `HerScreenInput` attach flakiness) — same overlay family.

### Ranked root-cause hypotheses

**Kurt — total miss (most → least likely)**

1. **`HerScreenInput` not attached / lost** after resize, re-embed, or VM recover → clicks hit VBox HWND → never Absolute-injected.
2. **COM inject or cursor park fails** (Mouse Integration clears Absolute if host pointer sits on the embed; park must leave Presence).
3. **Guest framebuffer size wrong** for stretch-fill map (slot treated as FB / stale 1280×800) → Absolute lands off interactive UI (can feel “dead”).
4. Overlay **z-order** under VBox GPU after SoftCursor sibling raise — intermittent.

**Victoria — lands but offset**

1. Soft-cursor is **Presence-drawn feedback**; real hit is Absolute — half-pixel / `Round` / Absolute **[+1,+1]** origin bias.
2. Soft-cursor / vision JPEG coords from **letterboxed** screenshot while inject uses **stretch-fill** FB map (or Auto-resize lag: slot ≠ FB).
3. Model aims from scaled still without full FB dimensions.

## 3. Goal & Success Criteria

1. Kurt click on Her screen center → guest click on the same UI control (3/3 with focus contested).
2. Victoria soft-cursor tip and Absolute hit agree within ~8–12 px at edges after DPI normalize.
3. One shared **slot → guest FB** map for overlay inject, soft-cursor paint, and agent Absolute.
4. Honest banner / log if `HerScreenInput` missing or COM inject fails (no silent deaf pane).
5. **No** OS `SetCursorPos` teleop as product path (CONTRA / PROP-14 soft-cursor lock).

## 4. Clarifying Q&A (open — need Kurt)

| # | Question | Why |
| --- | --- | --- |
| **Q1** | When you click and nothing happens: does the **hover badge / soft-cursor sibling** still track your pointer over Her screen? | Badge moving ⇒ overlay alive; badge dead ⇒ attach/z-order |
| **Q2** | Windows **Display scale** % on the Presence desk (100 / 125 / 150 / 200)? Multi-monitor? | DPI double-scale |
| **Q3** | Target is **VM desktop** (Ubuntu in `victoria-sandbox`), not Playwright Chromium-only pane? | Confirms COM path |
| **Q4** | After **Recover / re-embed / Host restart**, do Kurt clicks ever work briefly then die? | Favors overlay loss vs permanent map bug |

## 5. Thinktank seats (summary)

- **STRAT:** One stack, two modes — fix Kurt delivery first, then shared transform; intervene blocked until delivery works.
- **CONTRA:** Reject OS-mouse drive; kill if Kurt still deaf after contested-focus smoke or Victoria edge error stays >~12 px; soft-cursor without inject metrics is theater.
- **SYS:** Overlay styles/z-order + single Absolute pipeline; diagnostics: Spy++ which HWND gets `WM_LBUTTONDOWN`, log slot/FB/Absolute args, corner calibration.

## 6. Avenues

| ID | Avenue | Notes |
| --- | --- | --- |
| **A** | Diagnose attach: assert `HerScreenInput` child present; re-attach on resize/recover; honest “input overlay missing” | **First** for Kurt dead clicks |
| **B** | Single map: soft-cursor paint + Kurt `PostFromSurface` + Victoria Absolute share `VictoriaBrowserCoordMap` / FB from `IDisplay` | **First** for Victoria offset + Kurt near-miss |
| **C** | Harden park + COM inject logging (fail loud) | Mouse Integration clears |
| **D** | OS mouse drive into guest | **Rejected** |
| **E** | True HWND click-through into VBox | **Rejected** as primary — guest doesn’t take it; design doc outdated |

## 7. Recommended route

1. **PROP-19.1 QA+BED** — Home PC repro matrix: overlay present? WndProc sees down? Absolute args? guest FB vs slot? (Kurt + Victoria same session.)
2. **PROP-19.2 BED+FED** — Overlay lifecycle: attach/re-attach guarantees; z-order above VBox GPU; banner if missing.
3. **PROP-19.3 BED** — Unify coord contract (stretch-fill + live FB); soft-cursor tip = inject point; regression tests (slot≠FB center case already hinted in tests).
4. **PROP-19.4 BED** — Park/COM failure visibility; no silent success.
5. **PROP-19.5 QA** — Contested-focus Kurt 3/3; Victoria corner grid ≤ tolerance; wheel issue may share overlay owner (coordinate with ISSUE-20261014-001, don’t swallow it).

## 8. Risks & Kill Criteria

| Risk | Kill / mitigate |
| --- | --- |
| Fix map only; Kurt still deaf | Delivery gate before “Pass” |
| Reintroduce `WS_EX_TRANSPARENT` on full-pane child | Documented dead Her screen — forbid |
| OS mouse “fix” | Reject; steal Kurt’s pointer |
| Auto-resize races | Prefer `IDisplay` FB; never assume slot = FB |

## 9. Suggested PM Handoff (after Kurt Q1–Q4 or TT defaults)

- `prop_id`: `PROP-19-her-screen-operator-clicks`
- Splits: **19.1** diagnose matrix · **19.2** overlay lifecycle · **19.3** shared coord map · **19.4** inject honesty · **19.5** QA smoke
- TT defaults if Kurt defers: assume **VM embed**, ask only **Q1+Q2** on next smoke; start 19.1 immediately.

**TT recommendation:** Park for Kurt’s Q1–Q2 (overlay feel + DPI). Most likely: **Kurt = overlay/COM delivery**; **Victoria = shared map / Absolute vs paint**. Two tickets, one stack.
