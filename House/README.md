# House/ — thin apps (House Victoria)

## Ownership

| Tree | Owns | Does not own |
| --- | --- | --- |
| **Quarry** `C:\Users\kurtw\LLMOD\LLMOD-max-master` | Live Week-1 endpoint + extract patterns until cutover | Must not be the long-term self |
| **SoulCore/** (`..\SoulCore`) | Continuous self: memory, inference, Hermes, host loop | Thin UX / MCP / voice satellites |
| **House/** (this folder) | Thin client apps: chat desktop, later MCP/voice/Unreal bridge | Core memory / LLM orchestration |

## House.ChatDesktop (Presence + Settings)

.NET 8 **WPF** shell — **not** an overlay tray clone.

- **Presence:** chat transcript/input; alive/warm from `presence.status` (HTTP `/health` fallback); emotion strip from `emotion.snapshot`; **Correct…** panel sends `emotion.correct` (SoulCore persists + echoes snapshot); chat via `chat.send` → `chat.delta`/`chat.done`
- **Settings (day-one tabs):** Identity · Memory · Emotion (points to Presence Correct…; settings store still BED/DBD)
- **Protocol:** `SoulCoreWsClient` → `ws://127.0.0.1:7700/ws` (BED-021). No Hermes/Ollama calls from UI
- **Defaults:** localhost loopback only (`127.0.0.1:7700`). Optional env: `HOUSE_SOULCORE_HOST`, `HOUSE_SOULCORE_PORT`
- **Secrets:** none in this tree — no App.config keys from quarry

## UnrealBridge

Stub docs for SoulCore → UE `:8888` verbs: [`UnrealBridge/README.md`](UnrealBridge/README.md).

### Run

```powershell
# SoulCore Host must be up first for live chat
# (Host continues if UE :8888 is down)
dotnet run --project SoulCore\SoulCore.Host -c Release

# Presence shell
dotnet run --project House\House.ChatDesktop -c Release
```

### Build

Included in `..\SoulCore\SoulCore.sln`.

```powershell
dotnet build House\House.ChatDesktop\House.ChatDesktop.csproj -c Debug
# or
dotnet build SoulCore\SoulCore.sln -c Debug
```
