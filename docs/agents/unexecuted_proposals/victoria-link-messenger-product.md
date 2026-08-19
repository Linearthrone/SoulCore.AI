---
type: proposal
status: unexecuted
tt_id: TT-01
created: 2026-08-19
updated: 2026-08-19
title: Victoria Link — Messenger-class product (not a Host reskin)
need: Phone app that keeps chat, syncs with the computer, looks professional with depth/themes, and can actually talk/video — decide SoulCore vs House
related_fork: docs/agents/unexecuted_proposals/victoria-digits-sms-channel.md
sent_at:
pm_intake:
---

> **2026-08-19:** Daily phone chat **forked** to DIGITS SMS/MMS (`victoria-digits-sms-channel.md`). This Messenger-class Link rewrite is **deprioritized** unless the T-Mobile gateway kill-test fails.

## 1. Need / Want

The current Victoria Link Android app feels like a prototype console: chat **wipes when you leave the screen**, **desktop talk does not appear** on the phone, UI and Settings are **flat/dull**, and **voice / video conversation** is not real. Kurt wants something **professional, post-modern, current** — Messenger-class (including **floating bubbles**), with **depth, texture, and theme options**.

Open architecture question: **part of SoulCore itself, or another House Victoria Solution project?**

## 2. Goal & Success Criteria

- Leave Chat / Settings / Call / kill the app → **thread still there**.
- Same Kurt↔Victoria conversation on **phone and ChatDesktop** (hydrate on open).
- Default surface feels like a **person in a thread**, not an ops dashboard.
- Visual: **tactile / glass / Material You-class** depth; **theme packs** (not system dark/light only).
- Voice: **in-app conversation** (not PSTN). Video: honest — either **duplex** or labeled as **watch her**, never JPEG-poll sold as a video call.
- Overlay “chat heads”: **nice-to-have wave**, not the definition of “not bleh.”
- Repo: **brain in SoulCore, product app in House** — no third product, no Compose inside Host.

## 3. Context & Constraints

Verified 2026-08-19:

| Layer | Today |
| --- | --- |
| App | `House/House.CompanionAndroid/` Compose. Brand: Victoria Link. |
| Chat UI | `ChatScreen`: `remember { mutableStateListOf }` — composition death = empty thread. |
| Phone session | Hardcoded `sessionId=companion-android`. |
| Desktop | `House.ChatDesktop` Avalonia; `sessionId=presence-local`; local SQLite `%LOCALAPPDATA%/HouseVictoria/presence-chat.db`. |
| Host “history” | `ChatSessionHistoryStore` = **in-memory LLM context** (~40), not a product transcript. |
| Theme | Material3 gold/cream + system dark/light (`CompanionTheme.kt`). |
| Call | `VideoCallScreen` **JPEG polls** `/api/companion/v1/call/frame`. `webrtc.available` not live. TASK-192 Unreal waist-up still in flight. |
| Prior proposal | `remote-companion-phone-port.md` **partially-executed** (thin client landed). This brief is **productization**, not a greenfield port. |
| Remote | Host loopback (SEC-004); phone via Tailscale + companion token. |

Thinktank: STRAT, CONTRA, SYS, RISK, USER (2026-08-19).

## 4. Clarifying Q&A (answered)

From this intake:

- Chat clears on leave; no computer conversation on phone; UI/settings bleh.
- Wants Messenger capabilities **including floating bubbles**.
- Depth/texture + themes.
- Explicit: SoulCore vs other House project.

Still open — §9.

## 5. Avenues Explored

### Avenue A — One Victoria Thread (recommended spine)

Canonical **durable transcript on SoulCore.Host** (SQLite table, not episodic memory, not BED-158 RAM). One `conversationId` for Kurt↔Victoria. Desktop + phone **hydrate**; Room/SQLite on device is a **cache**. WS already fans out `chat.done`; add history cursor / `GET …/messages?after=`.

This is the only path that fixes “clears” and “not on the computer” as **one** bug.

### Avenue B — Skin first (rejected as lead)

Themes, elevation, Settings redesign while lists stay `remember`. Looks newer; first leave-screen still sucks. Allow a **parallel visual pass** only after A is contracted.

### Avenue C — New repo / Flutter-RN rewrite / UI in Host (rejected)

- **UI in SoulCore.Host:** Host is not a phone runtime; reverses architecture.
- **Greenfield repo outside Soul_Core:** protocol skew (LLMOD `:17890` fragment again).
- **Flutter/RN rewrite:** months of parity, still no sync/WebRTC. Avalonia desktop does not unify with Flutter.

Keep **`House.CompanionAndroid`**. Optional later `House.LinkContracts` if iOS needs a shared protocol module. That is still House, not SoulCore.

### Avenue D — Chat heads as P0 (rejected)

`SYSTEM_ALERT_WINDOW` + OEM battery + overlay-on-banking. Facebook heads ≠ in-thread bubbles. **v1: in-thread tactile bubbles + Android Notification Bubbles / FGS reply.** Overlay heads = wave 2, default off, never over `FLAG_SECURE` apps.

### Avenue E — JPEG poll = video (rejected as naming)

Keep poll as **watch-her** until Unreal can publish a media track. Real duplex = WebRTC (signaling days; Unreal encode **weeks**). Voice v1 can be **STT+TTS / continuous listen** on existing Host paths (desktop already has PTT) — cheaper than WebRTC.

## 6. Recommended Route

**House owns the app. SoulCore owns the conversation. Do not start a third product.**

| Layer | Lives in |
| --- | --- |
| Inference, memory, companion WS/REST, **canonical transcript**, call signaling | **SoulCore.Host** |
| Victoria Link UI (Compose), themes, bubbles, notifications | **`House/House.CompanionAndroid`** |
| Desktop Presence UI | **`House.ChatDesktop`** (same protocol + tokens, not shared widgets) |

**Wave 1 (must feel “not bleh”):**

1. Stop Compose-list-as-truth (ViewModel + hydrate).
2. Unify `conversationId` (retire user-visible split `companion-android` / `presence-local`).
3. Host durable transcript + both clients consume it.
4. Visual language: tactile dark default, glass optional, **theme packs** (tokens, same layout). Settings: **You & Victoria / Notifications & Call / Advanced** — not a lab dump.
5. In-thread bubbles with elevation/texture. Label Call honestly.

**Wave 2:** in-app voice conversation; Notification Bubbles; optional overlay heads after SEC.

**Wave 3:** real video (WebRTC + Unreal track) or keep JPEG as “see her.” PSTN never.

## 7. Alternatives (parked)

- Overlay chat heads (wave 2).
- iOS (after APIs freeze; no chat heads on iOS).
- LiveKit self-host vs P2P (only after Unreal can publish).
- New `House.VictoriaLink` folder rename — cosmetic; not required for Wave 1.

## 8. Risks & Kill Criteria

**Must-mitigate**

- Phone transcript encrypted at rest; `allowBackup` must not dump token/chat.
- Host is source of truth; first-connect must **not** dump full desk history onto an unpaired phone.
- No public TURN / Funnel / `0.0.0.0` “so video works” (SEC-004).
- Overlay never required for v1; never draw over banking/`FLAG_SECURE`.
- User camera vs avatar frames strictly split.
- Theme packs = local tokens, not remote CSS.

**Kill**

- Overlay permission as P0.
- JPEG poll accepted as “video call works.”
- Cross-device chat with no Host-durable store.
- `.kt` UI moved into `SoulCore.Host`.
- Greenfield repo without versioned companion protocol in this tree.
- Framework rewrite with no new Host contract.
- Theme explosion without a written material spec (SLOP).

**Seat dissent**

- USER wants overlay heads **parked**; Kurt named them explicitly — facilitator: **in-thread + Bubbles in Wave 1**, overlay as Wave 2 unless he insists P0.
- SYS: JPEG can stay for near-term Call UX if **labeled**. CONTRA: never call that video.

## 9. Open Questions for User / PM

1. **One thread** across PC + phone (same LLM context), or two threads that only **mirror** in an inbox?
2. **“Phone conversation”** = in-app voice to Victoria (Messenger), not a SIM/PSTN number — confirm?
3. **Floating bubbles:** Notification Bubbles (no draw-over permission) OK for v1, or is Facebook-style overlay **required** in the first ship?
4. **Video v1:** watch her (better JPEG / waist-up) vs wait for duplex WebRTC?

## 10. Suggested PM Handoff

- **Environment:** TINA-main when sent.
- **Likely roles:** BED-01 (Host transcript + conversationId + hydrate API), FED-01 Android (ViewModel, themes, Messenger shell, honest Call), FED-01 desktop (hydrate same API in ChatDesktop), SEC-01 (encrypted cache, backup, overlay/TURN), REX-01 (real call track later), QA-01 (kill app → history; desk message appears on phone).
- **Order:** BED transcript **before** FED skin. Do not ticket a rewrite. Do not ticket overlay as P0.
- **PM decide first:** Q1 (one thread) and Q3 (overlay vs bubbles). Architecture (House vs SoulCore) is **already decided in this brief** unless Kurt overrides.
