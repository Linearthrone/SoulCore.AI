---
type: proposal
id: PROP-COMPANION-01
from: TT-01
created: 2026-07-27
updated: 2026-08-02
status: partially-executed
title: Remote companion phone app — Victoria Link on SoulCore (thin client)
---

# Remote companion phone app — port to SoulCore

> **2026-08-02 update:** Scope expanded from text-only Phase 0 to **Victoria Link thin client** on SoulCore (proactive chat, ComfyUI MediaGen/Gallery, single Victoria + `contactId` stub). Phase 0 text+notifications shipped; Link waves A–C landed in tree under `House/House.CompanionAndroid/` + Host `/api/companion/v1`. No LLMOD overlay / `:17890`. No phone computer-use. Multi-persona UI deferred to a future external persona service.

## Why this exists

Victoria is becoming her own person — she has a persona, she is gaining tools,
and she will want to message the user about something she is thinking about,
something she is doing, or something she needs approved before continuing. The
user is not always at the desk. The phone is the natural channel for that.

LLMOD shipped an Android Kotlin companion (`AndroidRemoteCompanion/`) that
talked to `HouseVictoria.App` on `:17890` over HTTP. SoulCore replaced that
desktop host with `SoulCore.Host :7700` (WS + HTTP health) and the desktop
companion is now `House.ChatDesktop` (Avalonia). The phone app was left behind.

This proposal is the engineering path to bring the phone companion back —
text chat first (fast-tracked), audio as a follow-on wave.

Investigation date: 2026-07-27.

---

## Verified current state

| Area | Finding | Source |
| --- | --- | --- |
| Desktop companion | `House.ChatDesktop` (Avalonia, .NET 8) talks to `SoulCore.Host :7700` via WS | `House/House.ChatDesktop/` |
| Phone companion | **None** in SoulCore | repo search |
| LLMOD phone app | `AndroidRemoteCompanion/` (Kotlin), HTTP to `:17890`, chat + audio | `docs/agents/log/TASK-20260722-003-BED01-to-PM01.md:80` |
| LLMOD host API | `RemoteCompanionWebHost.cs` + `RemoteCompanionChatService.cs`; Bearer / `X-Api-Key` auth | `TASK-20260722-004-SEC01-to-PM01.md:80` |
| SoulCore protocol | WS frames: `chat.send`, `chat.delta`, `chat.done`, `emotion.snapshot`, `loop.want`, `presence.status` | `SoulCore/SoulCore.Protocol/SoulCoreFrame.cs` |
| Auth in SoulCore | Loopback-only by default; remote = Tailscale + token (SEC gate) | `soulcore-continuous-victoria-redesign.md:158`, `charter-lock-and-cutover-weekend-checklist.md:127` |
| Notifications (desktop) | Just added to `House.ChatDesktop` — ding on new assistant message when unfocused | `Services/NotificationService.cs` (this commit) |
| Notifications (phone) | **Not yet specified** — this proposal adds it | — |

### What the desktop companion already does (reference for feature parity)

- WS connect to `SoulCore.Host :7700`
- Send `chat.send`, receive `chat.delta` / `chat.done` (streaming bubbles)
- `emotion.snapshot` display + `emotion.correct` send
- `loop.want` display
- `presence.status` (alive / warm)
- `/health` polling (memory, unreal, charter, spend)
- **Notifications** (new): plays a sound when a new assistant message arrives
  while the window is unfocused/minimised; user-configurable `.wav` or OS beep

---

## Hard blockers

**None for text-chat.** The WS protocol is already on the Host. The phone app
is a new WS client + UI shell. The only gating item is a remote-access path
(loopback bind blocks a phone on Wi-Fi/cellular).

**B1 — Remote access path.**
`SoulCore.Host` binds loopback by default (SEC policy). A phone on the same
Wi-Fi needs either a LAN bind (SEC review) or a Tailscale serve/funnel. This
is a deployment decision, not a code blocker — the app code is identical
either way; only the connect URL changes.

---

## Proposed architecture

### Phase 0 — Text chat (fast-tracked)

```text
Phone (Kotlin/Compose)
  └─ WS client → SoulCore.Host :7700/ws
       ├─ chat.send  { text, sessionId }
       ├─ chat.delta / chat.done  (streaming)
       ├─ presence.status
       └─ emotion.snapshot
```

Reuse the LLMOD `AndroidRemoteCompanion` Kotlin shell, gut the HTTP client,
swap in a WS client (`OkHttpClient` + `WebSocketListener`). The chat UI,
auth token entry, and settings store can be carried over nearly verbatim.

### Notification feature (added to scope per user)

The phone app **must** include notifications so the user hears/sees when
Victoria sends a message while the phone is locked or the app is backgrounded.

Requirements:

1. **Foreground service** (Android) holding the WS connection while backgrounded.
2. **Push a local notification** with the assistant text snippet when a
   `chat.done` arrives and the app is not in the foreground.
3. **Sound** — default to the OS notification sound; user can pick a custom
   sound file in Settings (mirrors the desktop `NotificationService` design).
4. **Vibration** — optional, on by default, toggle in Settings.
5. **Notification tap** opens the chat conversation.
6. **Quiet hours** — optional (follow-on; not in Phase 0).

This mirrors the desktop `NotificationService` behaviour (ding on new
assistant message when unfocused) but uses Android notification channels
instead of `SoundPlayer`, and a foreground service instead of a window
focus check.

### Auth + remote access

- Same token model as LLMOD: Bearer / `X-Api-Key` header on WS upgrade.
- Connect URL configurable in Settings:
  - Default: `ws://127.0.0.1:7700/ws` (for on-device / emulator)
  - Tailscale: `wss://<host>.<tailnet>.ts.net/ws` (recommended)
  - LAN: `ws://<lan-ip>:7700/ws` (SEC review required)
- Token stored in Android Keystore (not plaintext SharedPreferences).

### What is NOT in Phase 0

- Audio (STT/TTS) — requires new SoulCore STT infrastructure; follow-on wave.
- Image/file transfer — follow-on.
- Video (WebRTC) — deferred to V1.1.
- Emotion correction UI — follow-on (desktop has it; phone can wait).

---

## Workstreams

### Phase 0 — Text chat + notifications (fast-tracked)

| # | Work | Role |
| --- | --- | --- |
| 0.1 | Fork LLMOD `AndroidRemoteCompanion` into `House/House.CompanionAndroid/`; strip HTTP client, keep UI shell + settings | FED |
| 0.2 | Add `OkHttpClient` WS client to `SoulCore.Host :7700/ws`; implement `chat.send` + `chat.delta`/`chat.done` streaming | FED |
| 0.3 | Token entry screen + Keystore storage; connect URL setting | FED |
| 0.4 | **Foreground service** for background WS persistence | FED |
| 0.5 | **Notification channel** + local notification on `chat.done` when backgrounded; custom sound path setting | FED |
| 0.6 | Vibration toggle + sound toggle in Settings | FED |
| 0.7 | SEC review: remote bind / Tailscale path; confirm token policy | SEC |
| 0.8 | OPS: document Tailscale serve setup for phone access | OPS |
| 0.9 | QA: on-device smoke (chat round-trip, notification fires when backgrounded, tap opens chat) | QA |

**Exit gate:** user can chat with Victoria from the phone, gets a ding +
notification when she replies while the app is backgrounded, and tap opens
the conversation.

### Phase 1 — Audio (follow-on)

| # | Work | Role |
| --- | --- | --- |
| 1.1 | SoulCore STT endpoint (Whisper / local) | BED |
| 1.2 | Phone audio capture + streaming to STT | FED |
| 1.3 | SoulCore TTS (Chatterbox) + phone playback | BED/FED |
| 1.4 | Push-to-talk + full-duplex modes | FED |
| 1.5 | QA: audio round-trip on device | QA |

**Exit gate:** user can voice-chat with Victoria from the phone.

### Phase 2 — Media + polish (later)

| # | Work | Role |
| --- | --- | --- |
| 2.1 | Image/file receive in chat (Victoria sends a screenshot / generated image) | FED/BED |
| 2.2 | Emotion snapshot strip + correction UI | FED |
| 2.3 | Quiet hours / do-not-disturb schedule | FED |
| 2.4 | Widget / quick-reply from notification | FED |

---

## Decisions needed from user

1. **Android only or iOS too?** LLMOD was Android-only. iOS adds a second
   WS client + notification implementation. Recommend Android first, iOS
   follow-on.
2. **Remote access path** — Tailscale (recommended, no LAN bind) vs.
   LAN bind (needs SEC review)? Recommend Tailscale.
3. **Phase 0 scope** — text chat + notifications only, or also include
   emotion snapshot strip? Recommend text + notifications only for speed.

---

## Risks

- **Background WS lifetime** — Android kills background services aggressively.
  The foreground service must show a persistent notification (Android 8+).
  This is standard but adds a notification the user always sees.
- **Token security** — storing the API token on the phone is the main attack
  surface. Keystore + Tailscale mitigates; a leaked token = chat access only
  (not filesystem tools — those are Host-side gated).
- **SEC gate** — any non-loopback bind requires SEC-01 sign-off before the
  phone can connect remotely. Do not skip.
- **LLMOD code reuse** — the Kotlin shell is reusable but the HTTP client,
  `HouseVictoria.App` endpoints, and `:17890` assumptions must be stripped
  completely. A partial strip will leave dead code pointing at a non-existent
  host.
