---
type: config
updated: 2026-07-23 (Wave-18 complete: E9 episodic reflection PASS)
---

# Product Root Declaration

| Field | Value |
| --- | --- |
| Execution route | **Avenue A — Soul-spine MVP** |
| Code home | `C:\Users\kurtw\Soul_Core` → `SoulCore/` + `House/` |
| Quarry | `C:\Users\kurtw\LLMOD\LLMOD-max-master` |
| Host | `http://127.0.0.1:7700/health` · `ws://127.0.0.1:7700/ws` (PID 63904) |
| UI | `House/House.ChatDesktop` → SoulCore WS only |
| Protocol | Shared `SoulCore/SoulCore.Protocol/` |
| Inference | Ollama primary (`:11434`); Hermes fallback (`:8642`); `num_predict=256` / `max_tokens=256` |
| Continuity suite | QA-036 **Pass** (C1–C6); QA-081 re-run **Pass** vs soak Host (492 probes, 0 errors) |
| Short soak | OPS-037 **Pass** — 15 min, 58/58 health; runbook `SoulCore/docs/soak-runbook.md` |
| 24h soak | OPS-063 **Pass** — stopped at 14h by user decision (2898 probes, 0 errors, disk stable) |
| Emotion | Conditions chat (QA-039); user correction E2E (QA-045); ISSUE-002 closed |
| Secrets | ISSUE-001 rotate **user-confirmed**; Host + `start-soulcore.ps1` load `SoulCore/.env` (`SOULCORE_*` only; BED-054 / OPS-055); ISSUE-001/002 closed; ISSUE-001(0723) closed (P3 convention) |
| Settings tabs | 7/7 north-star tabs: Identity, Memory, Emotion, Unreal, Safety/Charter, System, Voice/Video (FED-079) |
| Safety libs | CharterService + DriftWatcher + SpendMeter built (BED-080) + DI wired (BED-082) + **active in code paths** (BED-091); `/health` exposes `safety.drift` + `safety.spend` |
| E2E gates | E1 speak **Pass**; E2 set_emotion **Pass**; E3 loco **Pass**; E4 ack **Pass**; E5 chat.done **Pass**; E6 loop.want **Pass**; E7 play_animation **Pass** (QA-093); E8 look_at **Pass** (QA-096); E9 episodic reflection **Pass** (QA-099) |
| Unreal bridge | `HouseVictoriaBridge` plugin on `ws://127.0.0.1:8888`; all 5 verbs forwarding confirmed: speak/set_emotion/loco/play_animation/look_at |
| Charter | 10 anchors seeded (4 identity, 3 safety, 3 value) — calibration mode (`is_locked=0`, `source='seed'`) |
| Self-authored memory | SoulLoop writes `[Reflection]` episodic entries every 5th tick (source='self'); verified in SQLite (QA-099) |
| SoulLoop | `enabled=true` — **LIVE** (PID 63904, ticks at 60s); DriftWatcher active per tick; kill switch available |
| Unreal (canonical) | `C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject` → expected body WS `ws://127.0.0.1:8888` |
| North star | `...\SoulCore_Architecture_and_Development_Plan.md` |

## Open gates (user)

1. ~~Rotate A2E token + wire `.env`~~ — done 2026-07-23 (BED-054 / OPS-055)
2. ~~Canonical Unreal project for `:8888`~~ — frozen → MyProject; Host→UE adapter + hygiene clean (SLOP-062)
3. ~~Authorize 24h soak~~ — done; OPS-063 stopped at 14h by user (PASS)
4. ~~E3 loco hard-stop~~ — **CLEARED** (QA-089, 2026-07-23 21:06 UTC); ISSUE-002 closed
5. ~~SoulLoop enable decision~~ — **GO** (user-authorized 2026-07-23 21:10 UTC); SoulLoop LIVE with kill switch

## In flight (PM)

- ~~Wave14 BED-082 + OPS-083 + QA-084 E2E gates~~ — done, archived (2026-07-23)
- ~~Wave15 BED-085 (token limit) + BED-088 (loco dispatch) + QA-089 (E3 final)~~ — done, ISSUE-002 closed
- ~~SoulLoop enable~~ — LIVE (PID 63904, ticks firing, `soulLoop=enabled`)
- ~~Wave16 BED-091 (safety active) + BED-092 (play_anim) + QA-093 (verify)~~ — done, archived (2026-07-23)
- ~~Wave17 BED-094 (look_at) + BED-095 (charter seed) + QA-096 (verify)~~ — done, archived (2026-07-23)
- ~~Wave18 FED-097 (UI safety fields) + BED-098 (SoulLoop episodic) + QA-099 (verify)~~ — done, archived (2026-07-23)
- **Next**: Memory enrichment (real embeddings → chat context) + charter lock + drift remediation + soak #2

## Completed since last update

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
