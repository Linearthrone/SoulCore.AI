# SoulCore — continuous self / service (House Victoria)

## Bind / health / WebSocket

| Knob | Default | Notes |
| --- | --- | --- |
| Bind | `127.0.0.1` | SEC-004 V1 — localhost only |
| Port | `7700` | Override via `Host:Port` in appsettings or env |
| Health | `GET http://127.0.0.1:7700/health` | Includes Memory, WS URL, Unreal status |
| Tools | `GET/POST http://127.0.0.1:7700/settings/tools` | Session gates; desktop/browser capture + computer control default **on** (TASK-177) |
| Identity | `GET http://127.0.0.1:7700/settings/identity` | Display name (Victoria) + charter/identity anchors (read-only) |
| **WS** | `ws://127.0.0.1:7700/ws` | Same Kestrel port; path via `ChatWs:Path` |

```powershell
dotnet run --project SoulCore.Host
# then:
Invoke-WebRequest http://127.0.0.1:7700/health
```

### WS JSON frame schema (`SoulCore.Protocol`)

Envelope (Presence chat on `:7700/ws` — not the Unreal `:8888` wire shape):

```json
{
  "v": 1,
  "type": "chat.send | chat.delta | chat.done | emotion.correct | emotion.snapshot | presence.status | loop.tick | loop.tick.ok | loop.want | error | ping | pong",
  "id": "correlation-id",
  "ts": "ISO-8601",
  "payload": { }
}
```

| Type | Direction | Payload |
| --- | --- | --- |
| `chat.send` | client → Host | `{ "text", "sessionId?" }` |
| `chat.delta` | Host → client | `{ "text", "stub?", "provider?" }` — **post-hoc cumulative prefixes** after full generate (not a live token stream) |
| `chat.done` | Host → client | `{ "text", "stub?", "provider?" }` (`provider`: `ollama` / `hermes` / `stub`) |
| `emotion.correct` | client → Host | `{ "valence" (-1..1), "arousal" (0..1), "dominance" (0..1), "focus" (0..1), "note?" }` — persist via `IEmotionState.SetAsync`; optional note → episodic `source=correction` tagged `[emotion_correction]`; echo `emotion.snapshot` |
| `emotion.snapshot` | Host → client | `{ "valence", "arousal", "dominance", "focus", "label", "note?", "revision" }` |
| `presence.status` | Host → client | `{ "alive", "warm", "phase" }` |
| `loop.tick` | client → Host | `{}` — explicit scaffold tick (tests). When `SoulLoop:Enabled=false` → `error` / `soulloop.disabled`. When enabled → hub broadcasts `loop.want` + this socket gets `loop.tick.ok` (no second want echo) |
| `loop.tick.ok` | Host → client | `{ "ok": true }` — ack for `loop.tick`; does not carry want |
| `loop.want` | Host → client | `{ "want", "category?", "emotionLabel?", "valence?", "arousal?", "episodicCount?" }` — sole authoritative want shape (hub broadcast; no skinny echo) |
| `ping` / `pong` | either | `{}` |

On connect, Host sends `presence.status` + `emotion.snapshot` (handshake).
`chat.send` **reads** `IEmotionState` and injects a deterministic emotion system/context preamble into Ollama (`system`) / Hermes (`role=system`) before the user text (read-influence only; no post-turn emotion write-back in this ticket). Primary order via `ChatWs:PreferHermes`. Happy path waits for a **full non-streaming generate**, then emits post-hoc cumulative `chat.delta` slices (for bubble UX) followed by `chat.done` with real model text (not stub). This is **not** true token streaming; wiring Ollama/Hermes stream mode is a later ticket. After success, Host writes first-person episodic memory (`source=chat`) without blocking the reply on memory failure. If LLM is unreachable and `ChatWs:StubWhenModelDown=false` (default), Host sends an `error` frame (`chat.model_down`) instead of a silent stub success.

Types live in `SoulCore.Protocol` (`SoulCoreFrame` / `SoulCoreFrameTypes`). Payload JSON uses camelCase; House and Host both reference this project.

## Unreal bridge (outbound client)

| Knob | Default |
| --- | --- |
| `UnrealBridge:WsUrl` | `ws://house-victoria:8888` (Shadow over Tailscale) |
| `UnrealBridge:ConnectTimeoutSeconds` | `10` |
| `UnrealBridge:Enabled` | `true` |
| `UnrealBridge:ConnectOnStartup` | `true` (failures **must not** crash Host) |

Outbound verbs map through `UeVerbWireMapper` to UE-native wire frames: **plain** `speak <text>` / `move_avatar_relative <f> <r> <u>` for PlainArgs verbs, and `{ "type":"command", "payload":{ "name", "args" } }` for `play_animation` / `look` / `set_emotion` (not Presence `SoulCoreFrame`). Source of truth: `House/UnrealBridge/README.md` mapping table.

| SoulCore verb | UE wire | Notes |
| --- | --- | --- |
| `speak` | plain `speak <text>` | Mapped (PlainArgs) |
| `play_animation` | `command` / `play_animation` | Mapped |
| `look` | `command` / `autonomy` + `look_at_player` | Mapped; `LookAsync` payload ignored |
| `set_emotion` | `command` / `set_emotion` | Mapped (V/A/D/label); see UnrealBridge README |
| `loco` | plain `move_avatar_relative` | Mapped (forward/right/up cm; empty→50); see UnrealBridge README |

Canonical UE project (product freeze): `MyProject` — see `House/UnrealBridge/README.md`.

## Memory (SQLite)

| Knob | Default |
| --- | --- |
| `Memory:DbPath` | empty → `%LOCALAPPDATA%\SoulCore\memory\soulcore_memory.db` |

Applies embedded `Schema/001_schema.sql` + `Migrations/001_initial.sql` on first open. Not LLMOD `Data/`.

Emotion round-trip evidence:

```powershell
dotnet run --project SoulCore.Host -- --emotion-roundtrip
```

## SoulLoop scaffold (want → act) — kill switch

| Knob | Default | Notes |
| --- | --- | --- |
| `SoulLoop:Enabled` | **`false`** | **Kill switch.** When false, `ISoulLoop.TickAsync` is a no-op and the hosted timer stays idle. |
| `SoulLoop:TickIntervalSeconds` | `60` | Background timer interval only when Enabled=true |
| `SoulLoop:EpisodicRecallLimit` | `3` | Recent episodic rows summarized into the want string |

Scaffold only: reads emotion + recent episodic → proposes a **categorized want** (`want[settle|reconnect|savor|engage|clarify|recall|notice|reflect]: …`) and optionally broadcasts `loop.want` (includes `category`). Does **not** enable browser / MT4 / email / file acts. Unreal verbs are not driven from this loop.

```powershell
# Evidence: flag off → no want; flag on → category matrix + one tick emits a want
dotnet run --project SoulCore.Host -- --soul-loop-tick
dotnet run --project SoulCore.Host -- --soul-loop-tick --enabled
```

## Browser capture (native — no Hermes)

| Piece | Path / URL |
| --- | --- |
| Bridge | `BrowserCaptureBridge/bridge_server.py` → `http://127.0.0.1:17891/health` |
| Extension | `BrowserCaptureExtension/` — Chrome/Edge **Load unpacked** |
| Host backend | `Tools:BrowserBackend=native` (default) → `NativeBrowserBridge` |
| Start | `SoulCore/scripts/start-browser-bridge.ps1` (also via `ALLSTART.ps1`) |

Load unpacked once: `chrome://extensions` → Developer mode → Load unpacked → select repo `BrowserCaptureExtension`. Popup should show bridge connected. SoulCore tools: `browser_health`, `browser_capture_tab`, `browser_click`, `browser_type`, `browser_key`, `browser_scroll`.

## Inference / Hermes (quarry loopback)
## Inference (Ollama)

| Client | Default base | Feature flag |
| --- | --- | --- |
| Ollama (`OllamaInferenceClient`) | `http://127.0.0.1:11434` | `Inference:Enabled` |

When `Inference:Enabled=false`, Host registers a null stub. **Hermes is retired (BED-185)** —
Host always registers `NullHermesClient`, forces `Hermes:Enabled=false` / `PreferHermes=false`,
and remaps `BrowserBackend=hermes` → `none`. Open Chrome/websites with `desktop_open_app`
(chrome + URL). `ALLSTART.ps1` skips the gateway unless `-WithHermes`.

## Build

```powershell
dotnet build SoulCore/SoulCore.sln
```

## Continuity soak (pre-24h)

Ops runbook: [`docs/soak-runbook.md`](docs/soak-runbook.md)

```powershell
.\SoulCore\scripts\start-soulcore.ps1
.\SoulCore\scripts\soak-soulcore.ps1            # default 15 minutes, loopback health probes
.\SoulCore\scripts\stop-soulcore.ps1
```

Do not enable non-loopback binds. Full 24h soak requires an explicit product gate.

## Secrets

Never commit tokens. Use:

- Local file: `SoulCore/.env` (gitignored; copy from `.env.example`) — Host loads `SOULCORE_*` keys into process env before config bind; existing shell env wins
- Environment: `SOULCORE_A2E_TOKEN`, `SOULCORE_HERMES_API_KEY`, `SOULCORE_HF_TOKEN`, `SOULCORE_COMPANION_API_TOKEN`
- Or: `dotnet user-secrets` on `SoulCore.Host` in Development

`SOULCORE_COMPANION_API_TOKEN` (optional, ≥ 32 random chars recommended): when **set**, Host fail-closes `/ws` upgrades unless the client sends `Authorization: Bearer <token>` or `X-Api-Key: <token>`. When **unset**, local loopback desktop keeps the historical no-header trust model. Set this whenever Tailscale serve is used for phone companion. Never log the raw token; `/health` stays unauthenticated on loopback and must not expose secrets.

See `appsettings.Example.json` and `.env.example` (placeholders only). Do not commit real `.env` values.

## Projects

| Project | Role |
| --- | --- |
| SoulCore.Host | Entry + health + `/ws` chat handlers + DI |
| SoulCore.Core | Loop / emotion / charter abstractions |
| SoulCore.Memory | `SqliteMemoryStore` + DBD schema embeds |
| SoulCore.Inference | Ollama HTTP client |
| SoulCore.Hermes | Hermes OpenAI-compatible client |
| SoulCore.Adapters.Ws | WS frame schema + Presence hub + Unreal verb client |
| SoulCore.Config | Bind / Memory / Inference / Hermes / ChatWs / UnrealBridge options |
