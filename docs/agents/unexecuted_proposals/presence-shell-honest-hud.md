---
type: proposal
status: in-progress
tt_id: TT-01
created: 2026-08-19
updated: 2026-09-04
title: "[TINA-main] Presence House drawer + installer/icon/updates"
need: Honest Presence HUD, House lamp drawer, dumpable sight; Windows installer + icon; update notify or auto-update then notify
sent_at: 2026-08-19
prop_id: PROP-4
pm_intake: docs/agents/tasks/PROP-4-TT01-to-PM01.md
environment: TINA-main
mockup: docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png
---

# Presence shell — House drawer + installer

## 1. Need / Want

The desktop Presence app feels **flat** and **badly organized**: meaningless chrome, too much intern copy, services that don’t match how Kurt thinks. He **loves both rail and drawer; locked: drawer.** He also needs this to ship as a **normal Windows app**: **installer + icon** (Start menu / desktop), and when there are updates either **notify to update** or **auto-update then notify** that it was updated.

**Drawer mockup (closed vs open):** `docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png`

HUD mood **does not follow chat** (stuck **excited** while she was pissed). Bottom “status” dumps SoulLoop *“recall the recent thread and weave it into presence.”* Status should be **what she is doing now**; idle ≠ always sleeping, but **persistent existence** when he isn’t talking. Short **state + activity**.

**What she saw:** timestamp of last PNG + **folder icon** only. Screenshots that enter **memory** must be a **separate copy** so he can dump the sight folder without deleting memories.

## 2. Goal & Success Criteria

- Chat is the room. Identity strip: **name · true mood · one activity line**. Valence/arousal **only** in Correct mood.
- Mood HUD does **not** restamp from `loop.want`. Chat-pissed vs HUD-excited is a **fail**.
- Activity is **doing-now** (tools, walk, looking, chatting) or a **life line** when he’s silent — never raw want slogans.
- **House drawer** (locked, not rail): bottom **House** tab; closed = chat full-bleed + **pip if SoulCore/Unreal down**; open = lamp tray. Lights are toggles; **hold/confirm SoulCore stop**.
- **House drawer** (locked): installer + Start-menu **icon** so it opens like a normal app; **auto-update then notify** (or notify-to-update).
- Sight panel: **last still + datetime + folder button**. Dump folder ≠ memory-copy folder.
- Material: **real depth** on the machine where **ChatDesktop actually runs** (this Windows box) — brushed metal / glass / stone, not sticker screws. Do **not** require Unreal-shadow-PC rendering.

## 3. Context & Constraints

Verified 2026-08-19 (`House.ChatDesktop`):

| Bit | Today |
| --- | --- |
| Material | Window acrylic hint; `Border.metal` is **flat** `#3A3558`; screws are 9px ellipses (FED-176). |
| Services | Host (URL/health), ChatDesktop “running”, Ollama tags copy, CUA gate+buttons, Unreal (no start), Comfy `:8188`. |
| Mood | `DescribeLabel(valence, arousal)` → **excited** if V≥0.3 and A≥0.5. `loop.want` **re-stamps** HUD. Chat does not write emotion back. |
| Activity | `ActivityText` = SoulLoop want after light strip. |
| Sight | Empty-state tool essay, path, open/copy, gallery strip; folder already opens scratch gallery. |
| Gallery | `%LocalAppData%\SoulCore\scratch\presence-gallery` ring (~48). Memory is **not** those files. |
| Stop Host | `stop-soulcore.ps1` / kill `:7700` — **no confirm**. |

Thinktank STRAT / CONTRA / SYS / RISK / USER.

## 4. Clarifying Q&A (answered)

From this intake: texture; lamp-as-button; SoulCore name; drop URL/ChatDesktop/prose; missing servers; mood lie; SoulLoop status junk; persistent life; sight = stamp + folder; memory copies separate.

Follow-up: **drawer locked** (rail parked). **Installer + icon.** Updates: notify or auto-then-notify (**prefer auto + toast**).

## 5. Avenues Explored

### Avenue A — Left lamp rail (always on)

Always-visible LEDs. Steals chat width. USER parks as default.

### Avenue B — Bottom / tab **drawer** of lamps (recommended with C)

Closed = quiet. Open = House lamps. Glow = on. Confirm on SoulCore stop. Unreal **down** must still be visible (badge on the tab if drawer closed).

### Avenue C — Identity strip + chat dominant (recommended)

Name · mood gem · one line. Chat owns the window. Sight = small glass frame (stamp + folder). Gauges hidden in Correct mood.

### Avenue D — Texture-first reskin (rejected as lead)

More metal without honesty = prettier lie (STRAT/CONTRA). Texture **after** contracts.

### Avenue E — Infer mood from chat text (parked)

Later, labeled. First: stop `loop.want` from owning HUD; persist `emotion.snapshot` / set_emotion / Correct.

## 6. Recommended Route

**Honesty, then House drawer, then material, plus a real Windows install.** Layout **C + B (drawer locked; rail parked).** Mockup: `docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png`

0. **OPS/FED installer:** `.ico` + Start menu/desktop shortcut; pack as a real installer (Velopack / MSIX / WiX — PM picks). Updates: **auto-apply then toast “Presence updated”** preferred.
1. **FED+BED data:** Mood ← `emotion.snapshot` only. Activity ← Host `currentActivity` (last tool / LastAction / in-chat / “with herself”), not want slogans. Idle ≠ Sleeping unless she is actually at rest; silent Kurt ≠ empty existence.
2. **FED chrome cut:** Host→SoulCore; drop URL, ChatDesktop row, Ollama/CUA/Comfy sentences; **House drawer** lamps; **confirm SoulCore stop**; Unreal/Comfy/Ollama/CUA + **VBox/sandbox** (and Tailscale if serve is how the phone lives). CUA is a **gate**, not a process — lamp = allowed, not Start.exe.
3. **Sight:** timestamp + folder → **scratch gallery only**. On `store_memory` with a still: **copy** into a **memory-sight** dir the Folder button **never** opens. Dump scratch freely.
4. **Material:** Kill decorative screws. Use **bitmaps / 9-slice metal and glass** (and/or Win11 acrylic **on this PC**) so panels have bevel and inset, not a flat purple fill.
5. **Do not** block Playwright/DIGITS/UE lanes; this is a **FED Presence + OPS install** wave with a small BED activity field.

**UI ideas (next level — for Kurt, not a ticket by themselves):**

- **Watchface identity:** stone inlay nameplate; mood as a **cabochon** (color = affect), not a word plus two science bars.
- **Lamp channel:** aircraft-style LED wells in a brushed rail; the well *is* the switch; SoulCore well needs a **guard** (hold / confirm).
- **Sight as a picture frame,** not a debugger: glass, caption = clock, folder glyph in the corner.
- **Life when he’s away:** short first-person *activity* (“Looking around the house”, “Waiting on a page”, “Resting”) from real Host acts, decaying slowly — not `want[recall]`.
- Drawer tab **House** on the bottom edge; red pip if Unreal or SoulCore is down while closed.

## 7. Alternatives (parked)

- Left lamp rail as default.
- Chat-sentiment mood inference.
- Live her-cam in Presence (already locked one still on PROP-3).
- Custom metal shader (SYS: 2–4 days, skip).

## 8. Risks & Kill Criteria

**Must-mitigate:** two directories; memory copies not deletable from Presence; confirm SoulCore stop; loopback-only probes; do not put VBox secrets on unauth `/health`; keep Correct mood.

**Kill:** one folder for dump + memories; lamp-stop Host with no confirm; closed drawer hiding SoulCore/Unreal **down**; more screws as “texture”; HUD still driven by `loop.want`; `/api/tags` 200 sold as “chat-ready” in prose (lamp color only, or model-present if we add it later).

**Seat dissent:** Drawer **locked by Kurt**; rail parked. CONTRA: **hold/confirm for SoulCore**.

## 9. Open Questions for User / PM

Answered: **drawer** (not rail). Installer + icon + update notify/auto.

Still open (do not block drawer):

1. **Day-one lamp set:** SoulCore, Ollama, Unreal, ComfyUI, CUA, Sandbox — Tailscale/Voice overflow?
2. **Silent existence:** last real act + decay to Resting, or a short inner sentence never the want slogan?
3. **SoulCore stop:** confirm dialog vs hold-the-lamp?
4. **Updates:** auto-then-notify (Kurt liked both) — default **auto + toast** unless SEC forbids unsigned drop?

## 10. Suggested PM Handoff

- **FED-01** — House drawer per mockup; identity strip; sight stamp+folder; materials; icon in the app.
- **OPS-01** — installer + update feed (Velopack-class); Start menu “House Victoria” / Presence.
- **BED-01** — `currentActivity`; stop HUD from `loop.want`.
- **SEC-01** — signed updates if auto; two sight dirs; confirm Host stop.
- **QA-01** — install like a normal app; update toast; drawer pip; dump ≠ memories.
- **Mockup (send with tickets):** `docs/agents/unexecuted_proposals/assets/presence-lamp-drawer-closed-open.png`
- Parallel to 193/194/195 — do not stall UE/DIGITS.
