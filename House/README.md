# House/ — thin apps (House Victoria)

## Ownership

| Tree | Owns | Does not own |
| --- | --- | --- |
| **Quarry** `C:\Users\kurtw\LLMOD\LLMOD-max-master` | Live Week-1 endpoint + extract patterns until cutover | Must not be the long-term self |
| **SoulCore/** (`..\SoulCore`) | Continuous self: memory, inference, host loop | Thin UX / MCP / voice satellites |
| **House/** (this folder) | Thin client apps: chat desktop, later MCP/voice/Unreal bridge | Core memory / LLM orchestration |

## House.ChatDesktop (Presence + Settings)

.NET 8 **Avalonia** cross-platform shell (Linux, Windows, macOS) — **not** an overlay tray clone.
Targets `net8.0` (no Windows-only dependencies), so it builds and runs anywhere the .NET 8 SDK is available.

- **Presence:** chat transcript/input; alive/warm from `presence.status` (HTTP `/health` fallback); emotion strip from `emotion.snapshot`; **Correct…** panel sends `emotion.correct` (SoulCore persists + echoes snapshot); chat via `chat.send` → `chat.delta`/`chat.done`
- **Settings (day-one tabs):** Identity · Memory · Emotion (points to Presence Correct…; settings store still BED/DBD)
- **Protocol:** `SoulCoreWsClient` → `ws://127.0.0.1:7700/ws` (BED-021). UI talks only to SoulCore Host (no direct Ollama calls)
- **Local stack:** Ollama + SoulCore Host required for chat; optional browser bridge / ComfyUI / Unreal avatar
- **Defaults:** localhost loopback only (`127.0.0.1:7700`). Optional env: `HOUSE_SOULCORE_HOST`, `HOUSE_SOULCORE_PORT`
- **Secrets:** none in this tree — no App.config keys from quarry

## UnrealBridge

Stub docs for SoulCore → UE `:8888` verbs: [`UnrealBridge/README.md`](UnrealBridge/README.md).

### Run

Cross-platform (Linux/macOS/Windows) — the desktop shell needs a graphical display.

```bash
# SoulCore Host must be up first for live chat
# (Host continues if UE :8888 is down)
dotnet run --project SoulCore/SoulCore.Host -c Release

# Presence shell (Avalonia)
dotnet run --project House/House.ChatDesktop -c Release
```

### Build

Included in `../SoulCore/SoulCore.sln` and builds on any OS with the .NET 8 SDK.

```bash
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Debug
# or the whole solution
dotnet build SoulCore/SoulCore.sln -c Debug
```
