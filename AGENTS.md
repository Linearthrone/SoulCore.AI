# AGENTS.md

## Cursor Cloud specific instructions

SoulCore.AI ("House Victoria") is a single .NET 8 product: a persistent AI-companion
backend (`SoulCore.Host`, ASP.NET Core / Kestrel) that chats over a WebSocket protocol,
persists state in embedded SQLite, and optionally drives an Unreal Engine avatar. The
desktop client (`House/House.ChatDesktop`) is a thin GUI front-end built with
**Avalonia** (`net8.0`), so it is cross-platform (Linux, Windows, macOS).

Standard commands live in `SoulCore/README.md` (bind/health/WS, build, evidence CLIs).
Notes below are the non-obvious cloud/Linux caveats.

### Build / test / lint (Linux VM)
- The **entire** `dotnet build SoulCore/SoulCore.sln` builds on Linux (0 warnings),
  including the Avalonia `House.ChatDesktop`. You can also build individual projects, e.g.
  `dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj`.
- The Avalonia packages are pinned to the `11.3.x` line. Avalonia `12.x` was tried and its
  name generator did not emit `InitializeComponent`/`x:Name` fields under this SDK, so keep
  the desktop app on Avalonia 11.3.x unless you verify 12.x generates named controls.
- Tests: `dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj`
  (xUnit, 62 tests, no external services needed).
- Lint: there is no ESLint/analyzer config; the practical gate is a 0-warning build plus
  `dotnet format <project> --verify-no-changes`.

### Running the backend
- Run: `dotnet run --project SoulCore/SoulCore.Host -c Release`. Health: `GET
  http://127.0.0.1:7700/health`; WebSocket chat: `ws://127.0.0.1:7700/ws`.
- The Host **refuses any non-loopback bind** (SEC-004). Keep bind at `127.0.0.1`.
- The `SoulCore/scripts/*.ps1` startup/soak/E2E harnesses are PowerShell + Windows paths;
  `pwsh` is not installed here. On Linux, run the Host directly with `dotnet run` instead.
- Config overrides use env vars with the `SOULCORE_` prefix and `__` for nesting, e.g.
  `SOULCORE_Inference__Model`, `SOULCORE_ChatWs__StubWhenModelDown` (see `SoulCore/.env.example`).
  Secrets can also go in `SoulCore/.env` (gitignored).

### Running the desktop client (Avalonia GUI)
- Run: `dotnet run --project House/House.ChatDesktop -c Release`. It needs a graphical
  display; this VM has one at `DISPLAY=:1` (TigerVNC + xfce), so launch with
  `DISPLAY=:1 dotnet run ...`. It talks only to the Host on loopback (`/health` + `/ws`).
- Client endpoint overrides: `HOUSE_SOULCORE_HOST` / `HOUSE_SOULCORE_PORT`.

### Chat requires an LLM backend (gotcha)
- With defaults (`ChatWs:StubWhenModelDown=false`), a `chat.send` with no reachable LLM
  returns an `error` frame (`chat.model_down`), **not** a reply.
- The default `Inference:Model` in `appsettings.json` is a very large HF GGUF model that is
  not practical to pull in CI. To get real replies, run a local Ollama and override the
  model, e.g. start `ollama serve` (no systemd here — run it manually, e.g. in tmux), pull a
  small model like `qwen2.5:0.5b`, then run the Host with
  `SOULCORE_Inference__Model=qwen2.5:0.5b`.
- To exercise chat wiring without any LLM, set `SOULCORE_ChatWs__StubWhenModelDown=true` for
  deterministic stub replies (`provider=stub`).
- Built-in no-LLM evidence CLIs on the Host: `--emotion-roundtrip`, `--soul-loop-tick
  [--enabled]`, `--secrets-presence`.

### Optional services
- Ollama (`:11434`) for real chat (required for Victoria day-to-day).
- Browser capture bridge (`:17891`) optional for tab screenshots/control.
- Unreal Engine avatar bridge (`:8888`) is optional; the Host logs a warning and continues
  when it is unreachable (`unreal.connected=false` in `/health`).
