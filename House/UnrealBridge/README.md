# House.UnrealBridge â€” protocol stub

Thin documentation + verb contract for SoulCore â†’ Unreal Engine body control.
**No full UE plugin rewrite in this ticket** â€” Host owns the outbound client stub.

## Topology

```text
[Presence / House.ChatDesktop] â”€â”€wsâ”€â”€â–º SoulCore.Host :7700/ws
                                              â”‚
                                              â–¼ outbound (optional)
                                     Unreal WS server :8888
                                     (Shadow: house-victoria via Tailscale)
```

| Endpoint | Role | Default |
| --- | --- | --- |
| `ws://127.0.0.1:7700/ws` | Presence chat protocol (SoulCore **server**) | Loopback only (SEC-004) |
| `ws://house-victoria:8888` | Unreal body verbs (SoulCore **client**, Tailscale → Shadow) | Config: `UnrealBridge:WsUrl` |

Canonical Unreal project (author on Shadow via P4):

`C:\HouseVictoriaUE5.8\MyProject\MyProject.uproject` (KAIA workspace) / synced root on Shadow (`housevictoria_shadow`)

Expected body WS: `ws://house-victoria:8888` (Tailscale MagicDNS; override to loopback for local PIE).

## Outbound verb mapping (SoulCore â†’ UE wire) â€” BED-057

Host **no longer** sends Presence-shaped `{v,type,id,ts,payload}` to `:8888`.
`UnrealVerbClientStub` maps through `UeVerbWireMapper` to UE-native wire frames that
`HouseVictoriaBridge` / `ParseWebSocketMessage` understands: **plain** `speak <text>` /
`move_avatar_relative <f> <r> <u>` for PlainArgs verbs, and the `{ "type":"command", "payload":{ "name", "args" } }`
envelope for `play_animation` / `look` / `set_emotion`.

### Wire shape (sent)

```json
{
  "type": "command",
  "payload": { "name": "<ue-verb>", "args": { } }
}
```

### Mapping table

| SoulCore verb | UE wire | Notes |
| --- | --- | --- |
| `speak` | plain `speak <text>` | **BED-067:** BridgeServer reads **PlainArgs**, not JSON `payload.args.text` (QA-065: JSON ack `success:false`, plain `success:true`) |
| `play_animation` | `{ "type":"command", "payload":{ "name":"play_animation", "args":{ "name":"…" } } }` | **BED-119:** BridgeServer reads JSON `payload.args.name` (+ PlainArgs fallback). Ack `success:true` on parse; montage `/Game/Animations/Victoria/{name}` may still be missing (Wave 27). |
| `look` | `{ "type":"command", "payload":{ "name":"autonomy", "args":{ "command":"look_at_player" } } }` | Nearest documented UE path; `LookAsync` payload ignored |
| `set_emotion` | `{ "type":"command", "payload":{ "name":"set_emotion", "args":{ "valence", "arousal", "dominance", "label" } } }` | **BED-069:** UE accepts JSON + plain `set_emotion <label>`; ack `success:true` (visual stub OK). **BED-070:** Host maps `SetEmotionAsync` (disk; soak Host may be old until recycle). |
| `loco` | plain `move_avatar_relative <forward> <right> <up>` | **BED-072 / BED-117:** local +X/+Y/+Z cm → **NavMesh path-follow walk** (not teleport). Empty payload → forward=50 |
| `move_to` | plain `move_to <x> <y> <z>` | **BED-117:** absolute world cm → `AIController.MoveToLocation` |
| `stop` | plain `stop` | **BED-117:** `StopMovement` / abort path; Host chat keyword "stop"/"halt"/… |
| `move_avatar_absolute` | plain / JSON transform | **Debug teleport only** (`SetActorLocation` + `TeleportPhysics`) |

Logical SoulCore verb names remain in `UnrealVerbTypes`.

## Host behavior

- `UnrealBridge:Enabled=true` registers `UnrealVerbClientStub`
- `ConnectOnStartup=true` attempts connect; **failures are logged, Host continues**
- Chat path calls `set_emotion` + `speak` after `chat.done` (best-effort)
- Session pre-authorize for local Unreal body verbs (not always-confirm UI)

## Code

| Piece | Location |
| --- | --- |
| Verb client | `SoulCore/SoulCore.Adapters.Ws/UnrealVerbClientStub.cs` |
| Wire mapper | `SoulCore/SoulCore.Adapters.Ws/Protocol/UeVerbWireMapper.cs` |
| UE envelope | `SoulCore/SoulCore.Adapters.Ws/Protocol/UeCommandEnvelope.cs` |
| Verb type names | `SoulCore/SoulCore.Adapters.Ws/Protocol/UnrealVerbTypes.cs` |
| Options | `SoulCore/SoulCore.Config/UnrealBridgeOptions.cs` |

## MyProject runbook (OPS-056)

**Listen path: exists** â€” in-tree plugin, not SoulCore stub.

| Item | Path / value |
| --- | --- |
| Project | `C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject` |
| Engine | **UE 5.8** (`EngineAssociation: "5.8"`) |
| Plugin | `Plugins/HouseVictoriaBridge/` (`HouseVictoriaBridge.uplugin`) |
| Built module | `Plugins/HouseVictoriaBridge/Binaries/Win64/UnrealEditor-HouseVictoriaBridge.dll` |
| Default port | **8888** (`DefaultGame.ini` â†’ `WebSocketPort=8888`, `bAutoStartServer=True`) |
| Bind | `0.0.0.0` |
| Avatar map | `/Game/Home` â†’ `Content/Home.umap` (has `BP_MHC_Victoria*`) |
| Smoke client | `bridge_test_client.py` (repo root of MyProject) |
| Rebuild | `build_bridge.ps1` / `build_bridge.bat` â†’ UE 5.8 `Build.bat MyProjectEditor` |

### PIE as your grounded avatar (not the flying ghost) — BED-184

Stock GameMode uses flying `ADefaultPawn` (spectator “ghost”). Victoria is a
separate `BP_VictoriaCharacter` (tag `VictoriaAvatar`, AI-possessed). Your body
on the floor is not possessed until GameMode Default Pawn Class is set.

**Product lock:** PIE should start as Kurt’s grounded Character, not free-fly.

1. Open `/Game/Home` in UE 5.8 (Kurt’s body is **`BP_MHC_Kayleigh`** on the floor).
2. Run Editor Python: `tools/ue_nav/set_pie_player_pawn.py`  
   (finds `BP_MHC_Kayleigh`, creates `/Game/Blueprints/BP_HouseGameMode`, sets Default Pawn Class, PlayerStart, World GameMode Override).
3. Press Play — you should be Kayleigh, not the flying ghost; Victoria stays AI-controlled.
4. Manual fallback: World Settings → GameMode Override → Default Pawn Class = `BP_MHC_Kayleigh`  
   (must be a Pawn/Character subclass — bare MetaHuman Actor cannot be possessed).
5. Do **not** point Default Pawn at Victoria (`VictoriaAvatar` / `BP_VictoriaCharacter`).

Note: Victoria walk may still return API-ok with travel=0 in PIE (ISSUE-006 /
path-follow). Eyes (`victoria_eye_capture`) and Presence “What she saw” are
independent of that motion bug.

### Phone-call waist-up camera (mobile Call tab) — TASK-192

Companion Android **Call** polls Host `GET /api/companion/v1/call/frame`, which
sends Unreal command **`call_capture`** and expects:

```json
{"type":"call_frame","bytes_b64":"…","format":"png","width":720,"height":1280}
```

This is a **front-facing SceneCapture on Victoria** (waist-up / selfie phone
framing) — not `eye_capture` (outward). REX-01 owns the UE camera + bridge wire
(`docs/agents/tasks/TASK-20260817-192-PM01-to-REX01.md`). Helper:
`tools/ue_nav/setup_victoria_call_camera.py`. Do **not** attach this to the
Kayleigh player pawn.

### Launch so `:8888` listens

1. Open `MyProject.uproject` with **UE 5.8**  
   (`C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe`).
2. Load map **`/Game/Home`** manually (not the default OpenWorld template — avatar lives on Home).  
   Do **not** change `DefaultEngine.ini` `GameDefaultMap` / `EditorStartupMap` unless product agrees (rollback: restore OpenWorld entries).
3. Confirm plugin enabled: Edit → Plugins → **House Victoria Bridge**.
4. **Start PIE (required for healthy WS)** — Alt+P / toolbar Play, or Remote Control:
   `PUT http://127.0.0.1:30010/remote/object/call` with  
   `{"objectPath":"/Script/LevelEditor.Default__LevelEditorSubsystem","functionName":"EditorRequestBeginPlay","parameters":{},"generateTransaction":false}`  
   Reason: `HouseVictoriaBridge` ticks via `FTickableGameObject`; idle editor can show TCP `LISTENING` on `:8888` but **will not complete the WebSocket handshake** until PIE (or equivalent game tick) runs.
5. Console (optional): `hv.start` / `hv.status` / `hv.stop`.
6. Smoke: from MyProject root, `python bridge_test_client.py` → expects `ws://127.0.0.1:8888` + `status` reply (`"scene":"Home"`).
7. Port check: `netstat -ano | findstr :8888` (expect `LISTENING`).

### Heal sticky `:8888` (OPS-064) — TCP listens, handshake times out

Symptom: `netstat` shows `0.0.0.0:8888 LISTENING` plus `CLOSE_WAIT` / peer `FIN_WAIT_2`; `python bridge_test_client.py` → `TimeoutError: timed out during opening handshake`. Raw TCP connect succeeds; HTTP Upgrade gets no reply (server not ticking).

**Do not restart SoulCore Host on `:7700` if a soak (e.g. OPS-063) is running.**

Exact heal that worked (2026-07-23, editor left running):

1. Clear sticky half-closed sockets (Remote Control console exec):

   ```powershell
   $body = @{
     objectPath = "/Script/Engine.Default__KismetSystemLibrary"
     functionName = "ExecuteConsoleCommand"
     parameters = @{
       WorldContextObject = "/Engine/Transient.UnrealEditorEngine_0"
       Command = "hv.stop"   # then "hv.start"
       SpecificPlayer = $null
     }
     generateTransaction = $false
   } | ConvertTo-Json -Depth 5
   Invoke-WebRequest http://127.0.0.1:30010/remote/object/call -Method PUT -Body $body -ContentType 'application/json'
   ```

   Run once with `hv.stop`, wait ~2s, then `hv.start`. Confirm `CLOSE_WAIT` rows are gone.
2. **Start PIE** (step 4 above) — `hv.stop`/`hv.start` alone clears sockets but **does not** fix handshake without tick.
3. Re-smoke: `python bridge_test_client.py` → Pass when Connected + `status` JSON returns.
4. If Remote Control / PIE remote call unavailable: stop PIE if stuck, or restart **UnrealEditor only** (never `:7700`), open `/Game/Home`, Play, re-smoke.

### Native UE frame shape (what the plugin actually parses)

```json
{ "type": "status" }
```

```json
{
  "type": "command",
  "payload": { "name": "speak", "args": { "text": "hello" } }
}
```

**Speak caveat (QA-065 / BED-067):** the JSON `command`/`speak` envelope is parsed and acked, but BridgeServer still pulls speech text from **PlainArgs**. Host therefore sends plain text:

```text
speak hello
```

Host sends plain frames for `speak` and `loco` (`move_avatar_relative`); `play_animation` / `look` / `set_emotion` go out as the JSON `command` envelope (see Gap status below). Plain text also works on the UE side for: `play_animation wave`, `move_avatar_relative …`, `set_emotion calm`.

**`set_emotion` (BED-069):** UE accepts both:

```json
{"type":"command","payload":{"name":"set_emotion","args":{"valence":0.0,"arousal":0.0,"dominance":0.0,"label":"calm"}}}
```

```text
set_emotion calm
```

Ack shape: `{"type":"ack","command":"set_emotion","success":true}`. Visual may be stub (log + optional PlayAnimation).

### Gap status (post BED-072 loco)

Host outbound adapter: `speak` / `loco` → plain frames; `play_animation` / `look` / `set_emotion` → JSON command envelopes.
UE plugin (MyProject `HouseVictoriaBridge`): `set_emotion` accepted; `loco` aliases `move_avatar_relative` (source on disk — rebuild to activate JSON/`loco` alias; plain `move_avatar_relative` works on current PIE).

| SoulCore verb | Status after adapter |
| --- | --- |
| `speak` | Mapped → plain `speak <text>` (PlainArgs-compatible) |
| `play_animation` | Mapped → UE `command`/`play_animation` (**BED-119:** UE parses `args.name`; ack `success:true`; visual montage may wait for Phase 2 assets) |
| `look` | Mapped → UE `command`/`autonomy` + `look_at_player` |
| `set_emotion` | **UE verb live** (BED-069). Host mapper on disk (BED-070); activates after Host recycle. Label map: calm/happy/sad/angry → montage (missing = visual stub, ack `success:true`). |
| `loco` | **Mapped** → plain `move_avatar_relative <forward> <right> <up>` (BED-072; empty→50). Host on disk until recycle. |

If WS handshake is sticky while TCP listens, follow **Heal sticky `:8888` (OPS-064)** above (`hv.stop`/`hv.start` + **PIE**; editor restart only if needed). Idle `LISTENING` without PIE is not healthy.
