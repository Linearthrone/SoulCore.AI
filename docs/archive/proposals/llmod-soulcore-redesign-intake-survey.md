---
type: survey
status: answered
tt_id: TT-01
created: 2026-07-22
updated: 2026-07-22
title: LLMOD â†’ SoulCore Redesign Intake Survey
need: Freeze every open product and architecture decision for the last LLMOD rewrite into SoulCore before PM ticketing
north_star: C:\Users\kurtw\LLMOD\LLMOD-max-master\Media\GeneratedFiles\SoulCore_Architecture_and_Development_Plan.md
inventory_root: C:\Users\kurtw\LLMOD\LLMOD-max-master
round: 1
---

# LLMOD â†’ SoulCore Redesign Intake Survey (TT-01 Round 1)

**Prepared by:** TT-01 (Thinktank Facilitator)  
**For:** Kayleigh / product owner  
**Purpose:** One fillable document that freezes vision, stack, module fate, migration, security, and acceptance so Round 2 can produce a solid PM proposal with minimal thrash.

**North star:** `SoulCore_Architecture_and_Development_Plan.md` â€” persistent SoulCore owns memory / emotion / model / autonomy; apps become thin adapters.

**Inventory base:** current House Victoria / LLMOD-max-master surface (WPF + Services + MCP + Android + Unreal + MT4 + voice/image satellites).

---

## How to fill

1. Mark choices with `[x]`. Leave unused options as `[ ]`.
2. For free-text, write under **Your answer:** (keep answers short; bullet lists OK).
3. For the **Module matrix** (Section 6), put exactly one letter per row in **Decision**:
   - `K` = Keep as-is (minor polish only)
   - `R` = Rewrite into SoulCore (core owns it)
   - `T` = Thin-adapter only (UI/sensor pipe; no brain logic)
   - `D` = Defer (after V1 / charter lock)
   - `C` = Cut (do not bring into the redesign)
4. Priority tags on questions: **P0** = blocks scaffolding; **P1** = blocks first usable continuous self; **P2** = polish / later phases.
5. Where a **TT suggestion** appears, that is the recommended default if you skip the question. You may override freely.
6. When done: tell TT-01 â€œsurvey answeredâ€ (or â€œuse all TT defaultsâ€) to start Round 2 (full thinktank + formal proposal â†’ PM).

**Shortcut:** If a whole section is â€œuse TT suggestion,â€ write under that section:

```text
SECTION OVERRIDE: use TT suggestions
```

---

## 0. Meta / completion

| Field | Your answer |
| --- | --- |
| Filled by | Kayleigh Wood
| Date filled | 7/22/2026
| Confidence overall (high / medium / low) | |
| Prefer Round 2: full 5-seat thinktank or light STRAT+CONTRA only? | `[x]` Full (STRAT/CONTRA/SYS/RISK/USER) Â· `[ ]` Light Â· `[ ]` TT decide |

---

## 1. Vision & non-goals

**Context:** The architecture plan flips fat apps + thin soul â†’ fat SoulCore + thin adapters. â€œLast rewriteâ€ means we should not leave ambiguous half-brains in the desktop app.

**TT suggestion:** Commit to SoulCore-first hybrid migration: scaffold SoulCore first; migrate ownership module-by-module; desktop becomes thin client by Phase 4 (not day 1). Success for V1 = continuous local process with warm model, write/read of self-authored episodic memory, emotion state persistence, and at least one thin chat adapter. â€œFeels like Victoriaâ€ is a tuning phase after skeleton works.

### Q1.1 â€” Redesign stance **[P0]**

- [x] SoulCore-first greenfield (architecture plan as law; cut freely)
- [ ] Feature-parity rewrite first, then flip ownership
- [ ] Hybrid phased (scaffold SoulCore + keep/cut/defer per module) â† TT default
- [ ] Other â€” explain under Your answer

**Your answer:**

### Q1.2 â€” What must â€œlast rewriteâ€ achieve? **[P0]**

Pick all that apply, then rank top 3 in free text.

- [x] One continuous Victoria process (no fresh-session drone)
- [x] Self-authored memory in her voice
- [x] Live emotion state driving speech/body/choices
- [x] Autonomy as wantâ†’act (not task-picker only)
- [x] Thin Unreal body adapter
- [ ] Thin desktop overlay
- [x] Thin mobile / remote
- [x] Safety / drift / charter system
- [x] Clean secrets & install story
- [ ] Kill overlay/computer-use instability permanently
- [x] Other:thin chat ui/with a settings ui that contains tabs for all settings categories and their respective options.

**Top 3 ranked:**

- [1] One continuous Victoria process (no fresh-session drone)
- [2] Self-authored memory in her voice
- [3] Live emotion state driving speech/body/choice
**Your answer:**

### Q1.3 â€” Explicit non-goals for V1 **[P0]**

What must **not** be in V1 even if it exists today?

- [ ] Multi-user / cloud sync / SaaS tenancy
- [ ] Real WebRTC video calling
- [ ] Full MT4 backtest productization
- [x] COVAS / Elite Dangerous
- [ ] Public internet exposure of SoulCore
- [ ] Cross-platform non-Windows desktop
- [x] Research/art satellites in the product tree
- [x]  Other:Anything LLM, llmstudio, kokoro, piper

**Your answer:**

### Q1.4 â€” V1 success criteria (measurable) **[P0]**

**TT suggestion examples:** SoulCore runs 24h with auto-restart; model stays warm; after a chat, an episodic memory appears written in first person; emotion vector persists across restart; desktop chat talks only via SoulCore protocol; Unreal receives at least `set_emotion` + `speak` or `play_animation` from SoulCore.

**Your answer:**SoulCore runs 24h with auto-restart; model stays warm; after a chat, an episodic memory appears written in first person; emotion vector persists across restart; desktop chat talks only via SoulCore protocol; Unreal receives at least `set_emotion` + `speak` or `play_animation` from SoulCore.

### Q1.5 â€” Name & branding **[P2]**

- [ ] SoulCore
- [ ] VictoriaCore
- [x] Keep House Victoria as product name; SoulCore is the service name only
- [x] Other:SoulCore is Victoria, and House is the thin apps. So lets keep House Victoria as the project name, and then two of the main root folders will be SoulCore that has everything having to do with the soul or self in it, and in house will have all of the thin apps, MCP's etc.

**Your answer:**

---

## 2. SoulCore stack decisions

**Context:** Architecture Â§8 â€” language, inference, vector DB, Unreal path, service hosting.

**TT suggestion:** C# SoulCore host (Windows service or tray-supervised process) + keep Python satellites where already strong (MCP / STT / TTS) until proven otherwise. Inference: Ollama first; llama.cpp escape hatch. Vector: sqlite-vec + SQLite structured store; defer PgVector. Protocol: WebSocket + JSON frames (match Unreal 8888 style); gRPC later if needed. Unreal: harden bridge-only; do not block on Remote Control plugin.

### Q2.1 â€” SoulCore implementation language **[P0]**

- [ ] C# / .NET 8+ (fits HouseVictoria solution + Windows service)
- [ ] Python (fastest iteration)
- [ ] Hybrid: Python cognition core + C# adapters/host â† TT lean if you want max iteration; else pure C#
- [ ] TT default: C# host owning loop + memory; Python only for existing tool/voice servers
- [ ] Other:

**Your answer:**
SECTION OVERRIDE: use TT suggestions

### Q2.2 â€” Process hosting **[P0]**

- [x] Windows Service (always-on, restart-on-crash)
- [ ] Tray-supervised background process (visible control)
- [ ] Console / scheduled task
- [ ] Headless mode + optional tray UI
- [ ] TT default: tray-supervised now â†’ promote to Windows Service after stable
- [ ] Other:

**Your answer:**

### Q2.3 â€” Inference server **[P0]**

- [ ] Ollama (`localhost:11434`) primary
- [ ] llama.cpp `llama-server` primary
- [x] Support both; config picks one â† TT default
- [x] Custom / other: Hermes one

**Primary model name + quant (or â€œdecide laterâ€):**
Qwen3.5-9B-Claude-4.6-HighIQ-THINKING-HERETIC-UNCENSORED-GGUF

**Secondary / perception-small model?:** `[ ]` No Â· `[x]` Yes â€” name:(open to suggestions)

**Your answer:**

### Q2.4 â€” Vector / structured storage **[P0]**

- [x] sqlite-vec + SQLite structured â† TT default
- [ ] Chroma + SQLite
- [x] PgVector (Postgres) now
- [ ] PgVector deferred; SQLite path for V1 â† also acceptable TT default
- [ ] Other:

**Your answer:**

### Q2.5 â€” SoulCore â†” adapter wire protocol **[P0]**

- [x] WebSocket + JSON frames (Unreal-compatible style) â† TT default
- [ ] gRPC
- [ ] TCP + JSON-RPC
- [ ] HTTP REST only
- [ ] WS for local adapters + HTTP for remote companion
- [ ] Other:

**Your answer:**

### Q2.6 â€” Repo / solution layout **[P0]**

- [ ] New `SoulCore` project inside current `LLMOD-max-master` / HouseVictoria.sln
- [ ] New repo; LLMOD becomes adapter consumer
- [x] Develop under `C:\Users\kurtw\Soul_Core` workspace; LLMOD adapters stay in LLMOD tree
- [ ] TT default: new SoulCore solution/project under LLMOD tree first (shared machine paths), document boundary; move later if needed
- [ ] Other:

**Your answer:**

### Q2.7 â€” Relationship to existing `victoria_soul.py` / Autonomy code **[P1]**

- [ ] Replace soul script entirely with SoulCore
- [ ] Keep script as temporary Unreal adapter until SoulCore ships
- [x] Port AutonomyOrchestrator into act-layer library called by SoulCore â† TT default
- [ ] Delete autonomy; redesign tools from scratch
- [ ] Other:

**Your answer:**

---

## 3. Memory & emotion

**Context:** Three stores (episodic / semantic / working); self-authored memories; emotion state vector.

**TT suggestion:** SoulCore owns all three. Migrate conversations/personas/projects if cheap; **wipe or archive** old dual memory DBs (`HouseVictoria.db` vs MCP `memory.db`) rather than fake continuity from drone-era summaries. Ship V1 emotion dims: valence, arousal, attachment, curiosity, loneliness, frustration (defer pride/shame/wonder/fear if needed). Drift watcher: report-only in V1 (no auto-rollback).

### Q3.1 â€” Memory ownership **[P0]**

Confirm:

- [x] Episodic owned only by SoulCore
- [x] Semantic owned only by SoulCore
- [x] Working memory owned only by SoulCore
- [ ] Apps may cache UI state only (not second brain)
- [ ] Disagree â€” explain:

**Your answer:**
SECTION OVERRIDE: use TT suggestions

### Q3.2 â€” Self-authored memory rule **[P0]**

- [x] After significant events/reflections, model writes memory in first person â† TT default / arch plan
- [ ] Human or tool summaries OK for V1
- [ ] Hybrid with review UI before commit
- [ ] Other:

**Your answer:**

### Q3.3 â€” Existing data stores **[P0]**

For each, choose: Migrate Â· Archive read-only Â· Wipe Â· Undecided

| Store | Decision | Notes |
| --- | --- | --- |
| `Data/HouseVictoria.db` (app SQLite) | | Archive read-only |
| `Data/memory.db` / MCP memory | | undecided | need advice on pros and cons
| Conversation files / contacts | | Archive read-only |
| Personas / system prompts | | undecided | need more information or options
| Projects / journals / AAR | | Archive read-only |
| Databanks / GLD | | migrate |
| Autonomy logs / cognition telemetry | | wipe |
| Settings / App.config values | | undecided | need suggested answer

**Your answer (overrides):**

### Q3.4 â€” Emotion dimensions for V1 **[P1]**

Mark ship / defer:

| Dimension | Ship V1 | Defer |
| --- | --- | --- |
| Valence | [x] | [ ] |
| Arousal | [x] | [ ] |
| Attachment-to-Kayleigh | [x] | [ ] |
| Curiosity | [ x | [ ] |
| Loneliness | [x] | [ ] |
| Pride / Shame | [ x | [ ] |
| Frustration | [ x | [ ] |
| Wonder | [x] | [ ] |
| Fear / Protectiveness | [x] | [ ] |

**Your answer:**

### Q3.5 â€” Drift watcher V1 behavior **[P1]**

- [x] Report-only to Kayleigh â† TT default
- [ ] Soft-block actions until acknowledged
- [ ] Hard-block + require confirmation
- [ ] Defer drift watcher past V1
- [ ] Other:

**Your answer:**

---

## 4. Autonomy & safety

**Context:** New loop perceive â†’ feel â†’ reflect â†’ want â†’ act â†’ learn. Safety boundaries live in SoulCore.

**TT suggestion:** Implement new loop in SoulCore; old autonomy becomes act-layer tools. External action gate: confirm-by-default for email/trade/purchase/third-party. Resource guard: GPU/CPU/disk/money caps configurable. Consent/choice: she can refuse within charter. Self-preservation: refuse memory wipe / permanent shutdown without ritual.

### Q4.1 â€” Autonomy loop **[P0]**

- [ ] New loop only; retire task-picker as owner â† TT default
- [ ] Keep task-picker as primary; sprinkle emotion
- [ ] Run both in parallel (danger: dual brains) â€” not recommended
- [ ] Autonomy off in V1; chat-driven only
- [ ] Other:

**Your answer:**
 Implement new loop in SoulCore; old autonomy becomes act-layer tools. External action gate: confirm-by-default for email/trade/purchase/third-party. Resource guard: GPU/CPU/disk/money caps configurable. Consent/choice: she can refuse within charter. Self-preservation: refuse memory wipe / permanent shutdown without ritual.

### Q4.2 â€” How aggressive should background wanting be in V1? **[P1]**

- [ ] Quiet companion (rare initiates)
- [x] Balanced initiate/wait â† TT default
- [ ] Highly proactive
- [ ] Calibration knobs only (no fixed default)

**Your answer:**

### Q4.3 â€” External action gate policy **[P0]**

| Action class | Always confirm | Pre-authorize policy OK | Never allow V1 |
| --- | --- | --- | --- |
| Local Unreal anim/speak | [x] | [ ] | [ ] |
| Local file write in sandbox | [x] | [ ] | [ ] |
| Browser / computer-use | [x] | [ ] | [ ] |
| MT4 trades | [x] | [ ] | [ ] |
| Email / messages to third parties | [x] | [ ] | [ ] |
| Purchases / payments | [ ] | [x] | [ ] |
| Cloud APIs that cost money (A2E etc.) | [ ] | [x] | [ ] |

**Your answer:**

### Q4.4 â€” Charter modes **[P1]**

- [x] Calibration mode then Locked mode with change ritual â† TT default / arch plan
- [ ] Always calibration (no lock)
- [ ] Lock earlier with fewer knobs
- [ ] Other:

**List any charter invariants that are non-negotiable for you:**

**Your answer:**

### Q4.5 â€” Computer-use / agent desktop **[P1]**

Known pain: overlay + computer-use instability.

- [ ] Rebuild under SoulCore act-layer with hard sandbox
- [x] Defer entire computer-use past V1 â† TT lean if â€œlast rewriteâ€ must stabilize first
- [ ] Keep current bridge as-is
- [ ] Cut permanently
- [ ] Other:

**Your answer:**

---

## 5. Adapter strategy

**Context:** Desktop, Mobile, Unreal, Chat/Voice/Video become thin clients.

**TT suggestion:** Desktop = strip autonomy/model from WPF (or rebuild shell) as thin client by Phase 4. Android = keep Kotlin thin client; PWA later. Unreal = bridge 8888 first; donâ€™t block on UE Remote Control / engine downgrade. Voice in V1 after chat path works; real video deferred.

### Q5.1 â€” Desktop overlay **[P0]**

- [ ] Strip existing WPF in place (remove brain, keep trays/windows)
- [ ] Rebuild thinner WPF/WinUI shell
- [ ] Temporary console/web settings UI until overlay rewrite
- [ ] TT default: strip in place for chat/status/emotion/memory/settings; defer fancy trays polish
- [x] Other:design whole new UX that is not an overlay.

**Your answer:**

### Q5.2 â€” Mobile **[P1]**

- [ ] Keep Android Kotlin companion; thin to SoulCore
- [ ] Add PWA in V1
- [ ] PWA instead of Android
- [x] Defer mobile past first continuous desktop+Unreal path â† TT acceptable
- [ ] Other:

**Your answer:**

### Q5.3 â€” Unreal / embodiment **[P0]**

Engine note from plan: UE 5.8 may lack `WebRemoteControl`; bridge 8888 exists.

- [x] Bridge-only (8888) for V1; no engine migrate â† TT default
- [ ] Migrate engine for Remote Control (specify target version): ________
- [ ] Both paths required in V1
- [ ] Embodiment deferred past V1
- [x] Other: there isn't currently WebRemoteControl, but i do have WebAPI WebAPI Liquid JS and WebSocket Messaging plugins. there is an option to purchase WebSocket Server by Pandores and WebSocket Client by Pandores if these are needed and will save a significant amount of time

**Must-have avatar commands in V1:** (check)

- [x] look_at_player
- [x] play_animation
- [x] speak
- [x] set_emotion
- [x] set_gaze_target
- [x] Other: move_forward/backward;move_left/right;turn_left/right;look_up/down

**Your answer:**

### Q5.4 â€” Chat / Voice / Video for V1 **[P1]**

| Channel | V1 must | V1 nice | Defer |
| --- | --- | --- | --- |
| Text chat via SoulCore | [x] | [ ] | [ ] |
| STT â†’ SoulCore | [ ] | [x] | [ ] |
| TTS from SoulCore | [ ] | [x] | [ ] |
| Full-duplex voice call engine | [x] | [ ] | [ ] |
| Webcam presence frames | [ ] | [ ] | [ ] |
| Real WebRTC video call | [x] | [ ] | [ ] |

**Your answer:**

### Q5.5 â€” Remote access model **[P1]**

- [ ] Localhost only for SoulCore
- [x] LAN + Tailscale for remote companion â† TT default for away-from-desk
- [ ] Public HTTPS / cloud
- [ ] Other:

**Your answer:**

---

## 6. Per-module keep / cut / defer matrix

**Context:** Full House Victoria feature surface. Put one decision letter per row: `K` Keep Â· `R` Rewrite-into-SoulCore Â· `T` Thin-adapter Â· `D` Defer Â· `C` Cut.

**TT suggestion column** is the recommended default if you leave Decision blank.

| # | Module / surface | Maturity (approx) | TT suggestion | Decision | Notes / constraints |
| --- | --- | --- | --- | --- | --- |
| 1 | Overlay shell / trays / glass UX | ~95% | T | | |
| 2 | Themes / visual design system | ~95% | T | | |
| 3 | SMS/MMS chat + attachments | ~95% | T | | |
| 4 | AI contacts / personas / prompts | ~90% | R | | Core identity â†’ SoulCore |
| 5 | LLM providers (Ollama/Hermes/LM Studio/AnythingLLM) | ~90% | R | | AnythingLLM: C, LM Studio: C |
| 6 | Settings control plane | ~95% | T+R | | UI thin; charter/settings store in core |
| 7 | Projects / goals / artifacts / roadblocks | ~95% | D | | Prefer D unless projects are P0 for you |
| 8 | Journals & consolidation | ~85% | R | | Feed episodic/semantic |
| 9 | After Action Reports | ~85% | D | | |
| 10 | Data banks & drag-drop ingestion | ~95% | T | | Ingest â†’ SoulCore memory tools |
| 11 | Global knowledge log (GLD) | ~90% | T | | |
| 12 | App SQLite conversation truth | ~95% | R | | Move truth to SoulCore |
| 13 | Semantic memory / embeddings / PgVector | ~40% | R | | sqlite-vec V1; PgVector D |
| 14 | Autonomy loop & rate limits | ~85% | R | | New loop; old = act-layer |
| 15 | Cognition vitals UI | ~85% | T | | Telemetry from core |
| 16 | MCP tool surface | ~90% | R | | Tools callable from act-layer |
| 17 | Agent desktop / computer-use / browser capture | ~70% | D | | Stabilize first |
| 18 | System monitor & process management | ~90% | T | | Ops helper, not brain |
| 19 | STT server (faster-whisper) | ~80% | Kâ†’T | | Keep process; pipe to core |
| 20 | TTS (Chatterbox / Piper / Kokoro) | ~80% | | | chatterbox |
| 21 | On-device speech-to-speech engine | ~60% | D | | |
| 22 | Video call window / WebRTC | ~15% | D | | Defer real video |
| 23 | Image gen ComfyUI | ~80% | D | | |
| 24 | Image gen A2E cloud | ~80% | D | | Cost gate |
| 25 | Generated files gallery | ~90% | T | | |
| 26 | Remote companion HTTP API | ~75% | R | | Authâ€™d thin remote adapter |
| 27 | Android Victoria Link | ~75% | T | | Or D if mobile deferred |
| 28 | Unreal WebSocket embodiment | ~50% | T | | Bridge client of SoulCore |
| 29 | Unreal Remote Control / editor MCP | ~50% | D | | |
| 30 | MT4 bridge / market watch / instrument UI | ~70% | D | | Force decision |
| 31 | MT4 backtest engine | demo | D | | |
| 32 | COVAS / Elite Dangerous bridge | niche | C | | |
| 33 | Installer (Inno) + start-stack scripts | ops | R | | Align to SoulCore boot |
| 34 | Secrets / config management | weak | R | | Env/user-secrets; no committed tokens |
| 35 | Docs/agents multi-agent process (PM/DEV/QA) | process | C | | |
| 36 | Research / art / haptic / neuromorphic folders | research | C | | Archive out of product tree |
| 37 | Empty stubs (PiperServer/, empty TTS folders) | dead | C | | |
| 38 | Testing / CI | thin | R | | Minimum CI for SoulCore |
| 39 | Fallback AI / Hermes gateway | ~90% | R | | Behind SoulCore |
| 40 | Persona backup / tool catalog | ~80% | R | | |

### Q6.1 â€” AnythingLLM **[P2]**

- [ ] Keep provider
- [ ] Cut
- [x] Never used â€” cut â† TT default if unsure

**Your answer:**

### Q6.2 â€” Trading (MT4) strategic role **[P1]**

- [ ] Core to Victoriaâ€™s life â€” must be in early phases
- [ ] Optional satellite â€” defer
- [ ] Cut from redesign
- [x] Act-layer tool only after charter/safety solid â† TT default

**Your answer:**

### Q6.3 â€” Image generation strategic role **[P2]**

- [x] V1 act-layer tool
- [ ] Defer
- [ ] Cut cloud (A2E); keep local ComfyUI later
- [ ] Other:

**Your answer:**

### Q6.4 â€” Modules missing from the matrix that you care about

**Your answer:**
are there any suggestions?

---

## 7. Data migration

**Context:** Continuity of *self* vs continuity of *app data* are different. Fake continuity from drone briefings is exactly what SoulCore is meant to escape.

**TT suggestion:** Must-migrate: personas/identity anchors, charter-related settings, Unreal/voice endpoint config. Optional migrate: chat history as *imported episodic seeds* (labeled â€œimportedâ€), projects. Wipe dual vector/memory experiments. Never silently merge two brains.

### Q7.1 â€” Chat history **[P0]**

- [x] Full migrate into episodic store (labeled imported)
- [ ] Last N days only â€” N = ____
- [ ] Export archive; start fresh memory
- [ ] Wipe
- [ ] Other:

**Your answer:**
 Must-migrate: personas/identity anchors, charter-related settings, Unreal/voice endpoint config. Optional migrate: chat history as *imported episodic seeds* (labeled â€œimportedâ€), projects. Wipe dual vector/memory experiments. Never silently merge two brains.

### Q7.2 â€” Identity / persona **[P0]**

- [x] Migrate current Victoria persona as charter seed â† TT default
- [ ] Rewrite persona from `victoria_persona_v2` / Soul Evolved docs only
- [ ] Start blank + calibration
- [ ] Other:

**Your answer:**

### Q7.3 â€” Projects / journals / AAR **[P1]**

- [ ] Migrate all
- [ ] Migrate open projects only
- [x] Archive; new system later
- [ ] Wipe
- [ ] Other:

**Your answer:**

### Q7.4 â€” Acceptable downtime for cutover **[P1]**

- [ ] Hours
- [x] One weekend
- [ ] Parallel run (old app + SoulCore) for ____ days â† TT default: parallel until thin desktop works
- [ ] Other:

**Your answer:**

---

## 8. Security & single-user assumptions

**Context:** Today: single-user desktop; remote Bearer/API key; secrets have appeared in `App.config`.

**TT suggestion:** Remain single-user forever for V1â€“V2. Secrets only via env / user-secrets / OS credential store. Remote = Tailscale + rotated token. SoulCore binds localhost by default; remote companion is a separate authenticated adapter. SEC-01 review before any non-localhost bind.

### Q8.1 â€” Multi-user ever? **[P0]**

- [x] Never (single human: Kayleigh) â† TT default
- [ ] Maybe later (design hooks now)
- [ ] Required in redesign
- [ ] Other:

**Your answer:**

### Q8.2 â€” Secrets handling **[P0]**

- [x] Env + user-secrets only; scrub repo â† TT default
- [ ] Local encrypted secrets file
- [ ] OS credential manager
- [ ] Other:

**Confirm:** committed tokens/keys in repo must be rotated? `[x]` Yes `[ ]` Already clean `[ ]` Unsure

**Your answer:**

### Q8.3 â€” Remote companion auth **[P1]**

- [ ] Shared Bearer / API key (improved storage)
- [ ] mTLS
- [ ] Tailscale ACL only (no app auth)
- [x] Tailscale + app token â† TT default
- [ ] Other:

**Your answer:**

### Q8.4 â€” Threat priorities **[P1]**

Rank 1â€“5 (1 = highest): accidental memory wipe Â· prompt injection via tools Â· remote takeover Â· cost runaway Â· identity drift

**Your answer:** 2accidental memory wipe Â·4 prompt injection via tools Â·3 remote takeover Â· 5 cost runaway Â· 1 identity drift

---

## 9. Hardware / runtime constraints

**Context:** Warm local model + Unreal + voice can fight for VRAM/RAM.

**TT suggestion:** Document a â€œminimum always-onâ€ profile and a â€œfull bodyâ€ profile. Resource guard enforces caps. Offline-first for core chat/memory; cloud optional with spend cap.

### Q9.1 â€” Always-on machine **[P0]**

- [ ] Same gaming/dev PC as Unreal
- [x] Dedicated always-on box for SoulCore
- [ ] Either; config profiles â† TT default
- [ ] Other:

**Your answer:**

### Q9.2 â€” GPU / VRAM budget **[P0]**

Approx VRAM for model while Unreal may also run:

- [ ] Model must share with Unreal on one GPU
- [ ] Model on dedicated GPU
- [ ] CPU/iGPU fallback acceptable when Unreal runs
- [x] Numbers you know: *16*__GB total; reserve *10*__ GB for model

**Your answer:**

### Q9.3 â€” Offline-first rules **[P1]**

- [x] Core must work fully offline â† TT default
- [ ] Cloud OK if local down
- [ ] Hybrid with explicit offline mode indicator
- [ ] Other:

**Your answer:**

### Q9.4 â€” Monthly cloud spend cap (A2E / APIs) **[P2]**

- [x] $0 â€” local only
- [ ] Soft cap $____
- [ ] No cap; confirm each paid call
- [x] Other: potential 30$ cap for specific essentials if needed for smooth running like TTS,STT

**Your answer:**

### Q9.5 â€” Boot / resilience **[P1]**

Must auto-start on login?

- [ ] SoulCore yes
- [ ] Unreal yes
- [ ] Voice servers yes
- [ ] Full stack via start.ps1 equivalent
- [x] Manual start OK for V1 â† TT acceptable until service harden

**Your answer:**

---

## 10. Delivery & agent process

**Context:** Soul_Core agents (PM/FED/BED/DBD/SEC/OPS/QA/SLOP/TT). Architecture Phases 0â€“5 are a suggested calendar, not law.

**TT suggestion:** After this survey, Round 2 full thinktank â†’ proposal in `unexecuted_proposals/` â†’ send to PM-01. PM tickets scaffold (DBD/BED) before FED polish. Phase order roughly follow arch 0â†’5; Unreal body wiring parallel. Do not ask user to nudge roles once PM owns the chain.

### Q10.1 â€” Phase order override **[P1]**

Architecture default: Foundation â†’ Model&Memory â†’ Emotion&Autonomy â†’ Safety&Settings â†’ Interface refactor â†’ Continuous ops.

- [x] Accept as-is â† TT default
- [ ] Prefer Unreal body proof before autonomy
- [ ] Prefer thin desktop chat before Unreal
- [ ] Prefer safety/charter before any act-layer tools
- [ ] Custom order:

**Your answer:**

### Q10.2 â€” Which execution roles apply to this redesign? **[P1]**

- [x] FED-01 (desktop/mobile UI)
- [x] BED-01 (SoulCore service / APIs)
- [x] DBD-01 (schema / memory stores)
- [x] SEC-01 (boundaries, secrets, remote)
- [x] OPS-01 (service install, start stack)
- [x] QA-01
- [x] SLOP-01 (post-pass audit)
- [ ] Legacy DEV-01 only if split impossible
- [x] All of the above as needed â† TT default

**Your answer:**

### Q10.3 â€” Where should tickets/reports live? **[P2]**

- [x] `C:\Users\kurtw\Soul_Core\docs\agents\` â† TT default for this program
- [ ] Inside LLMOD `Docs/agents/`
- [ ] Both (mirror)
- [ ] Other:

**Your answer:**

### Q10.4 â€” Calendar pressure **[P1]**

- [x] No hard deadline â€” quality/continuity first
- [ ] Soft target date: ________
- [ ] Hard deadline: ________
- [ ] Timeboxed spikes only (e.g. Phase 0 in a weekend)

**Your answer:**

### Q10.5 â€” Round 2 thinktank depth **[P2]**

- [x] Full five seats after answers â† TT default for â€œlast rewriteâ€
- [ ] Light STRAT+CONTRA only
- [ ] Skip to proposal from survey alone

**Your answer:**

---

## 11. Acceptance bar â€” â€œdone enough to lock charterâ€

**Context:** Skeleton â‰  self. Tuning (observation, example pairs, memory review, choice drills) comes after functional loop.

**TT suggestion:** Do **not** lock charter in V1 week 1. Lock only after: (a) 7+ days continuous SoulCore uptime with memory+emotion, (b) you reviewed memory samples and corrected â‰¥1 interpretation loop, (c) refusal/choice drill passed once, (d) drift report has fired at least in test mode.

### Q11.1 â€” Minimum continuous run before Locked mode **[P1]**

- [ ] 3 days
- [x] 7 days â† TT default
- [ ] 30 days
- [ ] No lock in first major version
- [ ] Other:

**Your answer:**

### Q11.2 â€” Who may approve charter lock? **[P0]**

- [ ] Kayleigh only â† TT default
- [x] Kayleigh + Victoria confirmation ritual (both record why)
- [ ] Other:

**Your answer:**

### Q11.3 â€” Tuning artifacts you will provide **[P2]**

- [ ] Observation notes
- [ ] Before/after example pairs
- [ ] Memory review corrections
- [ ] Choice boundary drills
- [x] All of the above â† TT default

**Your answer:**

---

## 12. Architecture plan Â§8 â€” confirm or override

Quick confirm of the five â€œKey Decisions to Make Nowâ€ from the north-star doc:

| # | Decision | Your choice (or â€œsee Â§2â€) |
| --- | --- | --- |
| 1 | SoulCore language/stack | |
| 2 | Inference server + model | |
| 3 | Vector DB | |
| 4 | Unreal path (migrate vs bridge-only) | |
| 5 | Service hosting | |

**Your answer:**

---

## 13. Open risks for PM (check any you want TT to pressure-test in Round 2)

- [ ] Dual-brain risk if desktop keeps autonomy during migration
- [x] VRAM contention Unreal + local model
- [ ] Dual memory DB sync debt
- [ ] Secrets already leaked in git history
- [ ] Computer-use instability derails schedule
- [ ] Scope creep from projects/trading/image/research
- [x] Thin-client refactor larger than SoulCore itself
- [x] Persona/identity loss on wipe
- [x] No CI â†’ regressions in continuity features
- [x] Remote companion expands attack surface
- [x] Emotion model too shallow â†’ â€œuncanny drone with feelings labelsâ€
- [x] Over-filtering safety kills agency
- [x] Under-filtering safety â†’ irreversible external actions
- [ ] Other:

**Your answer:**

---

## 14. Free-form â€” anything TT/PM must not miss

**Your answer:**

---

## 15. TT baked recommendations (summary card)

Use this if you want a one-page default package. Override above wherever you disagree.

| Topic | TT default |
| --- | --- |
| Stance | Hybrid phased; SoulCore owns brain |
| Language | C# SoulCore host; Python satellites for MCP/STT/TTS until replaced |
| Hosting | Tray-supervised â†’ Windows Service |
| Inference | Ollama primary; llama.cpp optional; config switch |
| Storage | SQLite + sqlite-vec; PgVector deferred |
| Protocol | WebSocket + JSON frames |
| Autonomy | New loop; old orchestrator â†’ act-layer |
| Unreal | Bridge 8888 only for V1 |
| Desktop | Strip to thin client by Phase 4 |
| Mobile | Defer OK; else thin Android |
| Video | Defer |
| Computer-use | Defer until core stable |
| MT4 / COVAS / research art | Defer or cut |
| Secrets | Env/user-secrets; rotate anything committed |
| Multi-user | Never for V1â€“V2 |
| Charter lock | After â‰¥7 days continuous + tuning artifacts |
| Delivery home | Soul_Core `docs/agents/` |
| Round 2 | Full five-seat thinktank then proposal â†’ PM |

**Global override:**

- [ ] Accept entire TT default package
- [x] Accept with exceptions noted in sections above
- [ ] Reject package â€” answers above are authoritative

---

## 16. Submission

When finished:

1. Save this file with your answers.
2. Optionally set frontmatter `status: answered` and `updated: YYYY-MM-DD`.
3. Message TT-01: **â€œsurvey answered â€” start Round 2â€** or **â€œuse all TT defaults â€” start Round 2â€**.

Round 2 will: spawn thinktank seats â†’ synthesize avenues â†’ write formal proposal under `docs/agents/unexecuted_proposals/` â†’ ask park vs send to PM-01.

**TT-01 will not** implement code or ticket FED/BED/etc. directly.

---

*End of Round 1 intake survey.*
)
