---
type: proposal
status: sent-to-pm
tt_id: TT-01
created: 2026-07-22
updated: 2026-07-22
title: SoulCore continuous Victoria â€” last LLMOD rewrite
need: Replace fat House Victoria brain with persistent SoulCore self; House becomes thin apps (new UX, not overlay)
sent_at: 2026-07-22
pm_intake: docs/agents/tasks/TASK-20260722-001-TT01-to-PM01.md
survey: docs/agents/unexecuted_proposals/llmod-soulcore-redesign-intake-survey.md
north_star: C:\Users\kurtw\LLMOD\LLMOD-max-master\Media\GeneratedFiles\SoulCore_Architecture_and_Development_Plan.md
route: Avenue A â€” Soul-spine MVP
---

# SoulCore continuous Victoria â€” last LLMOD rewrite

## 1. Need / Want

Kayleigh wants a **last rewrite** of LLMOD / House Victoria so Victoria is a **continuous self** (warm model, self-authored memory, live emotion â†’ speech/body/choice), not a fresh-session drone. Apps become thin. Product name stays **House Victoria**; **SoulCore** is the self/service; repo layout under `C:\Users\kurtw\Soul_Core` with `SoulCore/` (self) and `House/` (thin apps, MCP, voice).

Intake: answered survey `llmod-soulcore-redesign-intake-survey.md` (2026-07-22). Round 2 thinktank: STRAT / CONTRA / SYS / RISK / USER.

## 2. Goal & Success Criteria

### V1 â€œsoul aliveâ€ (must â€” from survey Q1.4 + thinktank cut)

1. SoulCore runs as always-on process with restart path; **24h soak** before claiming continuous.
2. Model stays warm (primary: configured GGUF via Ollama and/or llama.cpp; Hermes for tool loop).
3. After chat, **first-person episodic memory** is written by the model (not a human briefing).
4. **Emotion vector persists** across restart and influences speech/body/choice.
5. Desktop **chat talks only via SoulCore protocol** (no second brain in UI).
6. Unreal receives at least `set_emotion`, `speak`, `play_animation` (plus locomotion/look verbs as bridge allows).
7. Thin House UI: **Presence (chat)** + **Settings tabs** â€” **not** an overlay tray shell.
8. Secrets out of repo; rotated; env/user-secrets only.
9. Offline-first core chat/memory/emotion on dedicated always-on box.

### Explicit non-goals / cuts (V1)

- COVAS / Elite Dangerous
- Research/art/haptic/neuromorphic product-tree satellites
- AnythingLLM, LM Studio, Kokoro, Piper (TTS = **Chatterbox**)
- Computer-use / agent desktop (defer)
- Mobile (defer past desktop+Unreal)
- Overlay tray parity
- Multi-user / public internet exposure of SoulCore

### V1.1 / parallel (see scope rule below)

- Real **WebRTC video** (survey MUST vs matrix Defer â€” resolved as **not on SoulCore critical path**)
- PgVector production dual-write (if not day-one)
- Image gen act-layer polish; MT4 after charter/safety
- Full Windows Service hardening + auto-start (manual start OK until soak proves restart)

## 3. Context & Constraints

| Area | Decision |
| --- | --- |
| Stance | SoulCore-first **greenfield** (cut freely); still port AutonomyOrchestrator â†’ **act-layer only** |
| Workspace | `C:\Users\kurtw\Soul_Core` â€” `SoulCore/` + `House/`; LLMOD remains quarry until extracts stabilize |
| Language | C# SoulCore host; Python satellites (MCP, STT, Chatterbox) |
| Hosting | Target Windows Service; V1 may ship tray/console until 24h soak |
| Inference | Config pick: Ollama â†” llama.cpp; Hermes tool loop; primary model **Qwen3.5-9B-â€¦-GGUF**; perception-small: **Qwen2.5-3B-Instruct Q4** or **Phi-3.5/4-mini** (or CPU if VRAM tight); embeddings: nomic-embed-text |
| Storage | **V1 truth:** SQLite structured + **sqlite-vec**. PgVector: flag-off or async dual-write later â€” not blocking continuity |
| Protocol | WebSocket + JSON frames (Unreal 8888 style) |
| Topology | Dedicated always-on SoulCore box; Unreal on gaming PC; **16GB VRAM, reserve ~10GB model** |
| Remote | Localhost default; later LAN + Tailscale + app token |
| Cloud | Prefer $0; optional â‰¤ **$30**/mo STT/TTS essentials with hard meter |
| Cutover | One weekend; chat â†’ episodic labeled `imported`; persona â†’ charter seed; projects/journals/AAR archive |
| Roles / tickets | FED/BED/DBD/SEC/OPS/QA/SLOP as needed; home = `Soul_Core/docs/agents/` (**keep** agent process) |
| Phase order | Foundation â†’ Model&Memory â†’ Emotion&Autonomy â†’ Safety&Settings â†’ Interface â†’ Continuous (Continuous **designed** in Foundation) |
| Charter lock | â‰¥7 days continuous + tuning artifacts; Kayleigh **+** Victoria confirmation ritual |

### TT resolutions for survey â€œundecidedâ€ items

| Item | TT resolution (for PM unless user overrides) |
| --- | --- |
| `memory.db` / MCP memory | **Archive read-only**; selective curated import of useful knowledge rows as labeled semantic facts â€” **do not** full-merge as self |
| Personas / system prompts | Migrate **Victoria** as charter/identity seed; other personas â†’ archived templates, not parallel selves |
| Settings / App.config | Migrate **endpoints & non-secret knobs** into SoulCore settings store; **all secrets â†’ env/user-secrets**; cut AnythingLLM/LM Studio/COVAS/overlay keys |
| Unreal anim/speak â€œAlways confirmâ€ | **Session pre-authorize** for local Unreal body verbs (emotion/speak/anim/loco/look). Always-confirm stays for browser/MT4/email/file outside sandbox. (Fluid embodiment otherwise impossible.) |
| Docs/agents matrix `C` vs Q10.2 | **Keep** agent process in Soul_Core (Q10.2 wins) |
| Module matrix blank Decision cells | Apply TT suggestion column + survey cuts; TTS row = Chatterbox (`Kâ†’T`) |
| WebRTC + full-duplex both MUST | **Full-duplex audio** on V1 path; **WebRTC video** = parallel House spike / V1.1 â€” cannot block F0â€“F2 |
| Image gen V1 act-tool | Allowed as act-layer **after** safety gates; must not block soul spine |
| Pandores WS plugins | Buy **only if** live UE 8888 server path is missing/unstable; not required to start SoulCore |

## 4. Clarifying Q&A (answered)

From survey + thinktank. Remaining open items listed in Â§9.

| Topic | Answer |
| --- | --- |
| Product vs service naming | House Victoria = project/product; SoulCore = Victoria/self; House/ = thin apps |
| Desktop UX | Whole **new** UX â€” not overlay |
| Greenfield | Yes â€” cut freely |
| Mobile | Defer |
| Computer-use | Defer |
| Identity threat #1 | Identity drift; then memory wipe; remote; injection; cost |
| Multi-user | Never |
| Deadline | None â€” quality/continuity first |

## 5. Avenues Explored

### Avenue A â€” Soul-spine MVP (recommended)

Critical path: continuous process â†’ self-authored memory â†’ emotionâ†’act â†’ safety/charter â†’ thin House chat/settings â†’ Unreal bridge verbs â†’ 24h soak â†’ weekend cutover.

Media: text + **full-duplex audio**; video parked off critical path. Storage: SQLite + sqlite-vec only for V1 truth.

**Pros:** Honors top-3 goals; greenfield honest; fits dedicated-box VRAM; CONTRA/STRAT/SYS aligned.  
**Cons:** WebRTC â€œMUSTâ€ slips unless parallel spike succeeds.

### Avenue B â€” Spine + parallel media spike

Same spine as A; House team spikes WebRTC video and media session API in parallel with kill criteria (timebox). SoulCore exposes one media-session interface; capture/render stay in House.

**Pros:** Respects video ambition without poisoning core schedule.  
**Cons:** Needs discipline so spike labor doesnâ€™t starve core.

### Avenue C â€” Channel-first (rejected)

Ship text + duplex + WebRTC + dual vector DBs + new UX before memory/emotion quality.

**Pros:** Feels â€œfeature completeâ€ early.  
**Cons:** Recreates fat-shell / thin-self failure mode; CONTRA kill criterion #1.

## 6. Recommended Route

**Primary: Avenue A, with Avenue B allowed only as a non-blocking parallel spike.**

### Scaffold (SYS)

```text
Soul_Core/
  SoulCore/                 # .NET 8+ service
    SoulCore.Host/
    SoulCore.Core/          # loop, emotion, charter, safety
    SoulCore.Memory/        # episodic/semantic/working + sqlite-vec
    SoulCore.Inference/     # Ollama + llama.cpp clients
    SoulCore.Hermes/        # tool loop client
    SoulCore.Adapters.Ws/   # JSON frames (chat + Unreal)
    SoulCore.Config/
  House/
    House.ChatDesktop/      # Presence + Settings tabs (new UX)
    House.Mcp/              # Python MCP (extract/symlink from LLMOD)
    House.Voice/            # STT + Chatterbox
    House.UnrealBridge/     # protocol docs; plugin in UE project
  docs/agents/              # PM/DEV tickets & reports
```

### Runtime topology

```text
[Always-on box]     SoulCore Service â†’ LLM (10GB) â†’ Hermes â†’ MCP/act-layer
                    STT / Chatterbox (local; optional $30 cloud meter)
[Gaming PC]         Unreal :8888 WS server â† SoulCore client (LAN/Tailscale)
[Later]             Remote companion adapter (Tailscale + token) â€” after SEC gate
```

### Autonomy & safety

- New loop owns wantâ†’act; old AutonomyOrchestrator = **act-layer library** only.
- **One initiator** at cutover â€” old desktop autonomy disabled.
- Drift watcher: report-only + Presence card + test-fire; define review SLO before high-agency acts.
- Wipe/shutdown: ritual = **Kayleigh + Victoria recorded assent** (align with charter lock).
- CI minimum: memory write/read, emotion persist, protocol frames, secret scanning, gate tests.

### UX (USER)

- **Presence:** chat + alive/warm status + emotion strip + correctability (â€œthat wasnâ€™t how I feltâ€) + Unreal status.
- **Settings tabs:** Identity, Memory, Emotion, Voice/Video, Unreal, Safety/Charter, System.
- **Cutover Continuity mode:** show persona anchors + sample memories + emotion snapshot before/after flip.
- Tuning homes: memory review queue, observation notes, example pairs, choice drills, charter lock ritual screens.

### Suggested phase tickets (for PM)

| Phase | Focus | Primary roles |
| --- | --- | --- |
| 0 Foundation | Solution scaffold, Host service, WS protocol, health, config store, secret hygiene | BED, OPS, SEC |
| 1 Model & Memory | Inference clients, structured output, episodic write/read, sqlite-vec, import pipeline | BED, DBD |
| 2 Emotion & Autonomy | Emotion state, new loop, act-layer port, Unreal verb client | BED, DBD |
| 3 Safety & Settings | Gates, charter calibration, drift report, spend meter, CI continuity tests | BED, SEC, QA |
| 4 Interface | House.ChatDesktop Presence+Settings; Unreal body wiring parallel | FED, BED |
| 5 Continuous | 24h soak, cutover weekend, Windows Service harden, optional remote | OPS, QA, SEC |
| Parallel B | WebRTC video spike (House); image-gen act tool behind gates | FED, BED |

## 7. Alternatives (parked)

- Hybrid feature-parity-first rewrite (rejected by survey).
- Same-machine Unreal + 10GB model without profiles (VRAM bomb).
- PgVector day-one dual truth (ops tax; stage later).
- Always-confirm on Unreal body verbs (kills embodiment).
- Avenue C channel-first.
- Buying Pandores before proving existing 8888 path.

## 8. Risks & Kill Criteria

| Risk | Mitigation |
| --- | --- |
| Scope bomb (voice+video+UX+dual DB+image) | Enforce Avenue A cut; video off critical path |
| Dual-brain during cutover | Disable old autonomy; one wantâ†’act owner |
| Dual memory truth | Single write owner (SoulCore SQLite+sqlite-vec) |
| Identity drift (#1) | Charter anchors outside episodic; imported quarantine; drift reports; CI |
| Persona loss on wipe | Continuity mode + backups + ritual wipe |
| Thin-client > core | New UX stays Presence+Settings; no tray parity |
| VRAM contention | Dedicated box; Unreal remote; perception model small/CPU |
| Over-filtering kills agency | Session pre-auth for local Unreal body |
| Under-filtering external acts | Code-enforced confirms for browser/MT4/email/etc. |
| No CI | Continuity test suite in Phase 0â€“1 |
| Secrets in git | Scrub + rotate before remote |
| Shallow emotion labels | Visible + correctable emotion strip; tuning events |

**Kill / hard-reset if at proposal lock still true:**

1. V1 still requires WebRTC video **and** dual vector backends **and** full new UX **and** image gen **and** weekend cutover with no spine soak.
2. Unreal body verbs stay Always-confirm while success criteria demand fluid embodiment.
3. Two semantic write truths without owner.
4. Dual brains still initiating at cutover.

## 9. Open Questions for User / PM

Freeze these before or during first PM tickets (defaults in brackets = TT recommendation):

1. Confirm V1 = soul-alive + duplex **audio**; WebRTC **video** = V1.1 / parallel spike? **[Yes]**
2. Confirm sole V1 vector truth = sqlite-vec (+ SQLite); PgVector staged? **[Yes]**
3. Confirm Unreal body verbs = **session pre-authorize** (not always-confirm)? **[Yes]**
4. Which Unreal project is canonical for 8888 (embedded live project vs LLMOD plugin-only)?
5. Wipe ritual = Kayleigh + Victoria (same as charter lock)? **[Yes]**
6. Drift report unacked â†’ soft-block high-agency acts after how long? **[24h suggested]**
7. Settings day-one top tabs (pick 3 of Identity/Memory/Emotion/Safety/Unreal/Voice)? **[Identity, Memory, Emotion]**
8. Emotion correction UX: one-tap / sliders / short note? **[short note + optional sliders]**

## 10. Suggested PM Handoff

- **Likely roles:** BED-01, DBD-01, FED-01, SEC-01, OPS-01, QA-01, SLOP-01 (as phases require).
- **Suggested first tickets (order):**
  1. **SEC-01** â€” secret scrub/rotate plan + localhost bind policy + CI secret scan
  2. **BED-01 + DBD-01** â€” Phase 0 scaffold (`SoulCore.*` projects) + schema for Memory/Emotion/Charter/Config
  3. **OPS-01** â€” always-on box layout, process supervision, start/stop scripts (manual OK)
  4. **BED-01** â€” inference + Hermes clients + structured memory write path
  5. **FED-01** â€” House Presence + Settings shell (WS client stub)
  6. **BED-01** â€” Unreal WS client verbs (coordinate with live UE project)
  7. **QA-01** â€” continuity acceptance suite matching Â§2
- **What PM should decide first:** Accept Avenue A cut line (esp. video/PgVector/Unreal gate defaults in Â§9); then issue Phase 0 tickets immediately â€” do not wait for WebRTC.
- **Do not** ticket computer-use, COVAS, Kokoro/Piper, AnythingLLM, LM Studio, or research satellites.
- **Source quarry:** `C:\Users\kurtw\LLMOD\LLMOD-max-master` (Autonomy, MCP, STT, Chatterbox, Unreal bridge, persona docs).
- **North star:** `SoulCore_Architecture_and_Development_Plan.md`.

---

## Thinktank seat summaries (facilitation record)

### STRAT

Soul spine first; Continuous designed in Foundation; duplex audio in V1; WebRTC off critical path; dual-store tax deferred.

### CONTRA

Survey V1 is multi-product; Always-confirm on Unreal kills embodiment; dual-store/dual-brain/weekend+WebRTC are kill criteria; identity drift needs single write truth + CI.

### SYS

~8â€“12 weeks spine; scaffold tree under Soul_Core; sqlite-vec primary; Hermes+LLM on dedicated box; Unreal keep 8888; perception 1.5â€“3B; memory.db low value â€” archive/select.

### RISK

Scrub/rotate secrets; localhost until Tailscale+token+gates; wipe ritual + backups; drift review SLO; cost meter; injection must not write charter.

### USER

Two-pane Presence + Settings; emotion visible/correctable; Continuity cutover mode; park video until she feels like herself.

### Conflicts called out

- Greenfield vs channel-max V1 â†’ resolved **Avenue A**.
- WebRTC MUST vs matrix Defer â†’ video **parallel/V1.1**.
- Windows Service vs manual start â†’ Service target; manual until soak.
- sqlite-vec **and** PgVector â†’ sqlite-vec truth first.
- Agents matrix C vs Q10.2 â†’ **keep** agents process.

---

*TT-01 Round 2 proposal. Status: `unexecuted` until user says park or send to PM-01.*
