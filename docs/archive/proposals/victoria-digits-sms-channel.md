---
type: proposal
prop_id: PROP-1-digits-sms-channel
status: accepted-pm-ticketed
tt_id: TT-01
created: 2026-08-19
updated: 2026-08-21
title: "[TINA-main] Victoria SMS/MMS on tablet gateway (was DIGITS)"
need: Give Victoria an SMS/MMS line Kurt can text from Messages; MMS screenshots of her work; Link shrinks to status + ComfyUI
sent_at: 2026-08-19
pm_intake: docs/agents/tasks/TASK-20260819-194-TT01-to-PM01.md
environment: TINA-main
forks: docs/agents/unexecuted_proposals/victoria-link-messenger-product.md
pm_tickets: PROP-1.1..1.6
pm_owner: WonderWoman (PM-01)
pm_note: 2026-08-21 — PROP-1.1 Pass on Samsung SM-X218U native MDN; DIGITS identity dropped; Avenue B unchanged
---

# Victoria SMS number — channel into One Thread

> **2026-08-21 lock:** Gateway = Samsung Tab **SM-X218U** native talk/text MDN (not DIGITS). PROP-1.1 **Pass**.

## 1. Need / Want

Kurt has an unused **T-Mobile DIGITS** line and wants to **keep it on T-Mobile**. He would **text her from Messages**. Victoria Link would then only need **server status** and **ComfyUI**. To see what she is doing, she can **push a screenshot as MMS** — no live phone stream. **Voice stays a later ship.**

Prior Link messenger-class brief is **deprioritized** as the daily phone chat UI. ChatDesktop remains the desk room.

## 2. Goal & Success Criteria

- Kurt texts **her E.164** from stock Messages; replies feel like a person (short, Host-up).
- Same **Host conversationId** as ChatDesktop (car SMS + desk work = one memory).
- v1: **SMS + MMS**, **Kurt’s number allowlisted**. Unknown inbound silent-drop.
- **See-her-work on the phone:** Victoria **sends an MMS screenshot** (Playwright/Presence frame), not a live stream in Link.
- Link **not gutted until** SMS/MMS round-trips work.
- **No PSTN/voice in this ship.** Voice is a separate later thinktank/ticket.
- Host stays loopback (SEC-004). **No Funnel** of `:7700`. **No port to Twilio** unless Avenue B is proven impossible.

## 3. Context & Constraints

| Fact | Detail |
| --- | --- |
| DIGITS | Consumer **multi-device line**, not a Twilio webhook API. T-Mobile BYON exists for **voice/WebRTC on a subscriber line**, not SMS bots. |
| SoulCore | No carrier SMS. “SMS” = ChatDesktop metaphor + phrase bank. `chat.send` is **WS only**. Push API is Victoria → Kurt. |
| Remote | Tailscale serve; Funnel on Host/chat was a **SEC Fail**. Twilio needs **public HTTPS** ≠ Tailscale serve. |
| STT/TTS | File Whisper + WAV — **not** PSTN RTP / Media Streams. |
| Call tab | JPEG Unreal frames — **not** a phone call. |

Thinktank STRAT / CONTRA / SYS / RISK / USER 2026-08-19. Web: DIGITS support pages + T-Mobile BYON (voice, not SMS webhooks).

## 4. Clarifying Q&A (answered)

- One shared PC+phone thread (user).
- Overlay heads not v1-required.
- Spare DIGITS exists; wants stock Messages/Phone; Link → status + Comfy.

Follow-up 2026-08-19:

| Q | A |
| --- | --- |
| Keep T-Mo vs port | **Keep on T-Mobile if possible.** |
| v1 media | **SMS/MMS first.** Voice later (still to be designed). |
| See her work on phone | **She pushes a screenshot as MMS.** No live Link stream required. |

## 5. Avenues Explored

### Avenue A — Port DIGITS MDN to Twilio/Telnyx (CPaaS)

Reliable SMS webhooks + later Media Streams. **Kills DIGITS multi-device** on that number. Needs **public webhook** (sidecar or tunnel **not** Host `:7700`). 10DLC if volume looks automated. Voice = extra weeks.

### Avenue B — Keep DIGITS; Android SMS/MMS gateway over Tailscale (**LOCKED**)

Dedicated cheap Android **or** DIGITS as a second line on a device that stays on: SMS/MMS received → POST Host on Tailscale; `chat.done` / media → `SmsManager` / MMS send. **Matches SEC-004.** Gateway must be able to **send MMS** (screenshot stills), not SMS-only. Weak on RCS typing. Device is a **third always-on**.

Port (Avenue A) only if B cannot send/receive on that MDN after a kill-test.

### Avenue C — DIGITS SIP / T-Mobile BYON as the SMS bot (parked)

BYON is **voice WebRTC**, not SMS. Consumer DIGITS is not a published SIP trunk. Do not ticket until Kurt has **real SIP/BYON creds**.

### Avenue D/E — Email-to-SMS, Google Voice, or a **new** Twilio number

Fails “**her** DIGITS number” (E) or stock Messages UX (D). Parked fallbacks.

### Avenue F — Messenger-class Link (prior proposal)

Still valid as a **parked** rich-client path. **Not** the daily phone chat if B ships. Couch “see her work” is **MMS screenshot**, so F’s live stream on phone is **out of v1**.

## 6. Recommended Route

**Locked: T-Mobile DIGITS stays. SMS/MMS v1. MMS screenshot = phone observer. Voice later. Avenue B.**

1. **Kill-test DIGITS inbound/outbound** on a gateway device (days): SMS both ways, then MMS still of a PNG.
2. **Host One Victoria Thread** — SMS/MMS is an adapter, not the store.
3. **Kurt-allowlist**, no tools from inbound SMS/MMS, no Funnel, no 911. Inbound MMS from Kurt = image into the thread (not executable). Inbound from others = drop. **Outbound MMS** = her screenshots / Comfy stills.
4. **Do not shrink Link** until SMS+MMS Pass. Then Link → health + ComfyUI.
5. **ChatDesktop** remains the live observer (Playwright stream). Phone gets **stills on request** (“send me a screenshot”).
6. **Voice** = later ticket; not this ship.

## 7. Alternatives (parked)

- Port to Twilio unless B fails a kill-test.
- PSTN/voice in this ship.
- Funnel/Cloudflare onto SoulCore.Host.
- Dual chat (Link Home **and** SMS) as two brains.
- Live phone stream of her browser (MMS stills instead).

## 8. Risks & Kill Criteria

**Must-mitigate:** Kurt-only allowlist; SMS/MMS ≠ tool loop; Tailscale token on gateway POSTs; dedicated DIGITS line not his daily SIM; Host-up policy; redact logs; no SoulLoop spam onto the carrier; no emergency origin; screenshot MMS **opt-in / on ask** (not every tool click); strip EXIF if needed; stills may show secrets — same Presence gallery rules.

**Kill:** Funnel/bind Host public; auto-reply to strangers; 911; number in git/health; inbound MMS executed as tools; bot on **primary** SMS graph; gut Link before SMS/MMS works; RCS scrape; porting as the *first* attempt while T-Mo still works.

**Seat dissent**

- USER: shrinking Link is the **right phone daily driver** *if* she’s a contact; desk stays ChatDesktop.
- CONTRA: shrinking Link **early** abandons Comfy/Call/desk sync; PSTN call is a **withdraw**.
- SYS: **B** if keep T-Mo + no public webhook; **A** if port + sidecar.
- RISK: allowlist is a **hard gate**; Twilio poll/sidecar not Host Funnel.
- STRAT: DIGITS is a **channel**; One Thread still required.

## 9. Open Questions for User / PM

Answered: keep T-Mo; SMS/MMS v1; MMS screenshot for phone observer; voice later.

Still open (do not block B kill-test):

1. **Gateway device:** spare Android that stays on, or DIGITS second line on the phone he already carries (OEM will kill background SMS)?
2. **Host off:** silence vs a one-time carrier/away SMS that is **not** Victoria’s voice?
3. **Daily Messages app** — iPhone or Android? (Affects RCS vs green-bubble only.)

## 10. Suggested PM Handoff

- **TINA-main** when sent. Do **not** ticket Link Messenger rewrite in the same wave.
- **Order:** (1) OPS/VBOX-or-device: DIGITS on a gateway Android + Tailscale. (2) BED: inbound HTTP→same chat pipeline + outbound SMS/MMS + `conversationId`. (3) BED: `send_screenshot_mms` (or reuse desktop screenshot → companion media → gateway MMS). (4) SEC: allowlist, no tools from SMS, no Funnel. (5) QA: Kurt SMS round-trip + MMS still. (6) FED Link: **after Pass**, strip to status + MediaGen.
- Voice: **not this ticket.**
- Port to Twilio: **only** if kill-test shows DIGITS cannot terminate to a gateway.
