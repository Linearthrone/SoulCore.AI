---
type: config
updated: 2026-07-29 (TINA: Phase E 140/141/158 Pass; OPS-143 Blocked→TT-159; QA-134 soft; QA-142 next)
---

# Product Root Declaration

| Field | Value |
| --- | --- |
| Execution route | **Avenue A — Soul-spine MVP** |
| Code home | `C:\Users\kurtw\Soul_Core` → `SoulCore/` + `House/` |
| Quarry | `C:\Users\kurtw\LLMOD\LLMOD-max-master` |
| Host | `http://127.0.0.1:7700/health` · `ws://127.0.0.1:7700/ws` (PID 45752) |
| UI | `House/House.ChatDesktop` → SoulCore WS only |
| Protocol | Shared `SoulCore/SoulCore.Protocol/` |
| Inference | Ollama primary (`:11434`, model `qwen2.5:14b` for tool-calling loop — BED-129 verifies; `gemma4:latest` retained as fallback for non-tool chat); Hermes fallback (`:8642`, restoring in OPS-143); `num_predict=256` / `max_tokens=256` |
| Continuity suite | QA-036 **Pass** (C1–C6); QA-081 re-run **Pass** vs soak Host (492 probes, 0 errors) |
| Short soak | OPS-037 **Pass** — 15 min, 58/58 health; runbook `SoulCore/docs/soak-runbook.md` |
| 24h soak | OPS-063 **Pass** — stopped at 14h by user decision (2898 probes, 0 errors, disk stable) |
| Emotion | Conditions chat (QA-039); user correction E2E (QA-045); ISSUE-002 closed |
| Secrets | ISSUE-001 rotate **user-confirmed**; Host + `start-soulcore.ps1` load `SoulCore/.env` (`SOULCORE_*` only; BED-054 / OPS-055); ISSUE-001/002 closed; ISSUE-001(0723) closed (P3 convention) |
| Settings tabs | 7/7 north-star tabs: Identity, Memory, Emotion, Unreal, Safety/Charter, System, Voice/Video (FED-079) |
| Safety libs | Charter + DriftWatcher + SpendMeter; spend **enforced** (E11); drift **soft-blocks Unreal** when SLO exceeded (E12); `/health/drift/ack` clears |
| E2E gates | E1–E14 **Pass**; E15 embedding backfill **Pass** (QA-111 — 174/174 vectors) |
| Unreal bridge | `HouseVictoriaBridge` plugin on `ws://127.0.0.1:8888`; all 5 verbs: speak/set_emotion/loco/play_animation/look_at |
| Charter | 10 anchors seeded — calibration mode (`is_locked=0`, `source='seed'`) |
| Self-authored memory | SoulLoop `[Reflection]` + **model-authored** chat episodics (E14) |
| Chat context | Identity + semantic recall (`nomic-embed-text`) + emotion preamble |
| Embeddings | Live; 768-d; `--backfill-embeddings` (BED-110); 174/174 covered; native sqlite-vec deferred |
| SoulLoop | `enabled=true` — **LIVE**; DriftWatcher active; kill switch available |
| Unreal (canonical) | `C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject` → expected body WS `ws://127.0.0.1:8888` |
| North star | `...\SoulCore_Architecture_and_Development_Plan.md` |

## Open gates (user)

1. ~~Rotate A2E token + wire `.env`~~ — done 2026-07-23 (BED-054 / OPS-055)
2. ~~Canonical Unreal project for `:8888`~~ — frozen → MyProject; Host→UE adapter + hygiene clean (SLOP-062)
3. ~~Authorize 24h soak~~ — done; OPS-063 stopped at 14h by user (PASS)
4. ~~E3 loco hard-stop~~ — **CLEARED** (QA-089, 2026-07-23 21:06 UTC); ISSUE-002 closed
5. ~~SoulLoop enable decision~~ — **GO** (user-authorized 2026-07-23 21:10 UTC); SoulLoop LIVE with kill switch
6. **Charter lock** — review 10 seeded anchors; say "lock the charter" when ready
7. **Unreal co-test** — open MyProject with body WS `:8888` for live speak/emote/loco/anim/look (superseded in part by Wave 26 embodiment walk gate QA-118)
8. **Soak #2** — authorize when you want a long soak with SoulLoop + embeddings + safety live
9. **Wave 26/27 embodiment decisions** — TT-01 / PROP-EMBODIMENT-01:
   - **Locomotion source**: Game Animation Sample (motion matching) vs Manny/Quinn blendspace — **provisional default in BED-115 = Manny/Quinn**; confirm or override
   - **Player embodiment**: grounded Kayleigh body vs keep free-fly `ADefaultPawn` — deferred to Wave 29 / Phase 4
   - ~~**Scope**: Phase 1 only vs Phase 1+2~~ — **CLEARED 2026-07-26**: user **"yes move phase 2 asap"** → Wave 27 Phase 2 (gestures + verb correctness + head gaze) **in flight now**, parallel with Phase 1; do **not** wait for QA-118
10. **Wave 27 Phase 3 agent-loop decisions** — TT-01 / PROP-AGENT-LOOP-01:
   - ~~**Tool-calling model**: `gemma4` vs `qwen2.5:14b`~~ — **CLEARED 2026-07-26**: user chose **`qwen2.5:14b`** (being pulled; appsettings updated; BED-129 verifies)
   - ~~**Backend**: native C# vs Hermes~~ — **CLEARED 2026-07-26**: user chose **both** (native C# Ollama tool-loop + restored Hermes tool-loop gateway)
   - ~~**Scope**: Phase A+B only vs all 6 waves~~ — **CLEARED 2026-07-26**: user chose **all 6 waves** (A–F all ticketed 125–145)
   - **Security gates** (design requirements on BED-135/138/133, not user blockers): session opt-in desktop, per-trade MT4 confirm, whitelisted FS roots
11. **Wave 28 phone companion** — TT-146 / PROP-COMPANION-01:
   - ~~Android vs iOS~~ — **CLEARED 2026-07-27 (PM default)**: Android first
   - ~~Remote path~~ — **CLEARED 2026-07-27 (PM default)**: Tailscale serve (no LAN bind)
   - ~~Phase 0 scope~~ — **CLEARED 2026-07-27 (PM default)**: text chat + notifications only

## In flight (PM)

- **2026-07-29 TINA:** BED-140 **Pass** (PR #3) · DBD-157 **Pass** (PR #4) archived → next BED-141 · BED-158 · OPS-143 · QA-134 — `reports/TASK-20260729-PM01-cold-start-patrol.md`
- **Merge order:** PR #4 (mig 003) **before** PR #3 (mig 004) — both touch `SqliteMemoryStore.cs`
- **Held (no UE / no adb):** BED-116/117 · QA-118 · BED-121 re-probe · QA-123 · QA-154
- ~~Wave14 BED-082 + OPS-083 + QA-084 E2E gates~~ — done, archived (2026-07-23)
- ~~Wave15 BED-085 (token limit) + BED-088 (loco dispatch) + QA-089 (E3 final)~~ — done, ISSUE-002 closed
- ~~SoulLoop enable~~ — LIVE (PID 63904, ticks firing, `soulLoop=enabled`)
- ~~Wave16 BED-091 (safety active) + BED-092 (play_anim) + QA-093 (verify)~~ — done, archived (2026-07-23)
- ~~Wave17 BED-094 (look_at) + BED-095 (charter seed) + QA-096 (verify)~~ — done, archived (2026-07-23)
- ~~Wave18 FED-097 (UI safety fields) + BED-098 (SoulLoop episodic) + QA-099 (verify)~~ — done, archived (2026-07-23)
- ~~Wave19 BED-100 (memory+charter chat context) + QA-101 (E10 verify)~~ — done, archived (2026-07-23)
- ~~Wave20 BED-102 (spend/token cap gate) + QA-103 (E11 verify)~~ — done, archived (2026-07-24)
- ~~Wave21 BED-104 (drift soft-block) + QA-105 (E12 verify)~~ — done, archived (2026-07-24)
- ~~Wave22 BED-106 (embeddings) + QA-107 (E13 semantic recall)~~ — done, archived (2026-07-24)
- ~~Wave23 BED-108 (model-authored episodic) + QA-109 (E14)~~ — done, archived (2026-07-25)
- ~~Wave24 BED-110 (embedding backfill) + QA-111 (E15)~~ — done, archived (2026-07-25)
- ~~Wave25 OPS-112 (start-soulcore Ollama/embed preflight)~~ — done, archived (2026-07-25)
- **Wave 26 — Victoria embodiment Phase 1 ("She walks")**:
  - ~~BED-114~~ **Pass** (archived) — `BP_VictoriaCharacter` + `VictoriaAvatar` on Home
  - ~~BED-115~~ **Pass** (archived 2026-07-27) — Manny→MH loco + `ABP_Victoria_Locomotion` + **DefaultSlot**; AnimClass assigned
  - **BED-116 STARTED 2026-07-27** (NavMesh) → then BED-117 → QA-118
- **Wave 27 — Phase 2**:
  - ~~BED-119~~ **Pass** · ~~BED-120~~ **Pass** · ~~BED-122~~ **Pass** (archived)
  - **BED-121 Partial** — montages exist; AC-3 unblocked by 115 DefaultSlot → re-probe `play_animation` then QA-123
- **Wave 27 — Phase 3 agent loop**:
  - Phase A ~~125–129~~ **Pass** (archived) · ~~BED-131~~ **Pass** (archived; live recall demo)
  - **QA-130 STARTED 2026-07-27** (formal gate; Host was DOWN at handoff — QA starts Host via `start-soulcore.ps1`)
  - ~~BED-132~~ **Pass** (archived 2026-07-27) — five body tools; `move_to` interim relative loco until BED-117 (ISSUE-20260727-003)
  - ~~BED-133~~ **Pass** (archived 2026-07-27) — `list_tools` / `system_info` / scoped FS; DI cycle closed
  - ~~QA-130~~ **Pass** (archived; AC7 closed by ~~BED-156~~ **Pass**) — Phase A agency gate cleared; ISSUE-001 Fixed
  - QA-134 gated on embodiment walk (117) for real `move_to` (wave/recall can soft-smoke earlier)
  - Phases C–F still queued (135–145); do not start C/D until OPS-143
- **Wave 28 — Phone companion Phase 0** (from TT-146):
  - Tickets FED-147…151 + SEC-152 + OPS-153 + QA-154 issued; reply archived `log/TASK-20260727-146-PM01-to-TT01.md`
  - ~~FED-147~~ **Pass** (archived) — `House/House.CompanionAndroid/` Compose shell
  - ~~FED-148~~ **Pass** (archived) — OkHttp WS `chat.send` + delta/done streaming
  - ~~FED-149~~ **Pass** (archived) — Keystore token + Bearer/`X-Api-Key` (aligned BED-155)
  - ~~FED-150~~ **Pass** (archived) — FGS keeps WS alive + connected notification
  - ~~FED-151~~ **Pass** (archived) — `chat.done` background `victoria_replies` alerts
  - **QA-154 STARTED 2026-07-27** (Phase 0 phone exit gate)
  - ~~SEC-152~~ **Pass (conditional)** archived — Tailscale+Keystore OK; **LAN bind Fail**
  - ~~BED-155~~ **Pass** (archived) — fail-closed `/ws` via `SOULCORE_COMPANION_API_TOKEN` (Bearer / X-Api-Key)
  - ~~OPS-153~~ **Pass** (archived) — runbook `docs/runbooks/tailscale-serve-soulcore.md` + `tailscale-serve-soulcore.ps1`
  - QA-154 gated on 148–151 (+ token set when using Tailscale serve)
- **Blocked on user**: charter lock · soak #2 · Wave 26 loco source confirm · player pawn (Phase 4)

## Completed since last update

- **2026-07-27**: Archived Pass pairs 114, 122, 124–129, 131; TT-146 phone companion ticketed (147–154)
- **BED-122**: Head/eye gaze IK Pass — `UVictoriaGazeComponent`; archived
- **BED-114**: `BP_VictoriaCharacter` Pass — archived
- **BED-120**: look/autonomy `args.command` parse Pass — archived
- **BED-119**: `play_animation` JSON `args.name` parse Pass — archived
- **Wave 27 Phase 2 pull-forward (user GO 2026-07-26)**: tickets BED-121/122 + QA-123; PM note `reports/TASK-20260726-PM01-phase2-pull-forward.md`; BED-119/120/121 handed to BED-01
- **TT-113 → PM**: Embodiment proposal accepted; Wave 26 Phase 1 tickets BED-114…QA-118 + parallel BED-119/120; reply `reports/TASK-20260726-113-PM01-to-TT01.md`
- **BED-085**: Added `MaxTokens` / `num_predict` to Ollama + Hermes inference clients to prevent endless generation
- **BED-088**: Added keyword-based loco intent dispatch to `HandleChatSendAsync` — `DetectLocoIntent` parses user text, calls `_unreal.LocoAsync({forward,right,up})`
- **QA-086**: Identified stale `bin/Debug` appsettings deployment gap (fixed by rebuild)
- **QA-087**: E1 PASS (speak forwarded to UE); E3 FAIL (missing loco dispatch path); identified test-harness `ReceiveAsync` cancel bug
- **QA-089**: E3 **PASS** — `move_avatar_relative 50 0 0` forwarded to UE; E1 re-confirmed PASS; ISSUE-002 closed; hard-stop gate CLEARED
- **TASK-090 (SoulLoop enable)**: `SoulLoop.Enabled=true`; Host recycled; first tick at 60s confirmed; `soulLoop=enabled` in health; kill switch available
- **BED-091**: Wired DriftWatcher into SoulLoop tick (records drift each cycle, `driftAlert` flag on want frame) + SpendMeter into inference path (estimates tokens after each call) + `/health` exposes `safety.drift` + `safety.spend`
- **BED-092**: Added `DetectAnimationIntent` keyword dispatch to `HandleChatSendAsync` — 12 animation keywords mapped to UE `play_animation` frames
- **QA-093**: All Wave-16 gates **Pass** — safety endpoint fields live, SpendMeter records 62/88 tokens after inference, E7 play_animation PASS (`anim=wave` forwarded), E1/E3 no regression
- **BED-094**: Added `DetectLookIntent` keyword dispatch — completes full UE verb set (speak/set_emotion/loco/play_animation/look_at)
- **BED-095**: Seeded 10 Victoria charter anchors (4 identity, 3 safety, 3 value) into live SQLite DB via idempotent `seed-charter-anchors.ps1`
- **QA-096**: All Wave-17 gates **Pass** — E8 look_at PASS (`look_at_player` dispatched), 10 charter anchors verified in DB, E1/E3/E7 no regression
- **FED-097**: Desktop `SoulCoreHealthClient` + `SoulCoreHealthSnapshot` extended to parse `safety.drift`, `safety.spend`, `soulLoop.enabled` from `/health`; `ApplySystemStatus` displays real color-coded status (no more placeholders)
- **BED-098**: `SoulLoopScaffold.TickAsync` writes a deterministic first-person `[Reflection]` episodic memory every 5th tick (configurable via `ReflectionIntervalTicks`); verified in SQLite (QA-099)
- **QA-099**: All Wave-18 gates **Pass** — E9 episodic reflection PASS (1 row, `source='self'`, `[Reflection] I am feeling neutral...`), `/health` safety fields live, E1/E3/E7/E8 no regression. Note: episodic reflection log emits at Debug level (DB row is authoritative proof).
- **BED-100**: Wired `ICharter` + episodic recall into `ChatWebSocketHandler`; `BuildContextPreamble` → `[Identity]` → `[Memory]` → emotion (2000-char budget)
- **QA-101**: Wave-19 **Pass** — Host recycled (PID 65376); E10 context enrichment live; E9b reflection log at Information; E1/E3/E7/E8/E9 no regression
- **BED-102**: Spend gate — `CapExceeded` (USD and/or `MonthlyTokenCap`) refuses inference with `chat.spend_cap` before CompleteChatAsync
- **QA-103**: Wave-20 **Pass** — E11 gate with MonthlyTokenCap=50 → `chat.spend_cap`; Host restored (PID 58976); E1 Pass with capExceeded=false
- **BED-104**: Drift threshold 1.15 + enqueue only when exceeded; Unreal soft-block on SLO; `POST /health/drift/ack`
- **QA-105**: Wave-21 **Pass** — E12 soft-block (no loco while SLO exceeded); ack restores loco; Host restored (PID 57356)
- **BED-106**: Ollama embeddings + cosine `RecallSimilarAsync`; chat uses semantic recall with recent-recall fallback
- **QA-107**: Wave-22 **Pass** — E13 seeded QUOKKA-7 + 768-d vector; paraphrase recall returned QUOKKA-7; E1 smoke Pass
- **BED-108**: Model-authored first-person chat episodic (`EpisodicMemoryPrompt`, 96-token cap); template fallback on failure
- **QA-109**: Wave-23 **Pass** — E14 episodic id 172 first-person (not template); E1 + E13 soft recall Pass; note: pulled `nomic-embed-text` after it was missing
- **BED-110**: `--backfill-embeddings` CLI; live run filled 163/163 including id 172
- **QA-111**: Wave-24 **Pass** — E15: 174/174 vectors; id 172 dims=768; E13 soft recall Pass
- **OPS-112**: `start-soulcore.ps1` advisory preflight for Ollama + `nomic-embed-text`; opt-in `-PullEmbedModel`
