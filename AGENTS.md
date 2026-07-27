# AGENTS.md

## Cursor Cloud specific instructions

SoulCore.AI ("House Victoria") is a single .NET 8 product: a persistent AI-companion
backend (`SoulCore.Host`, ASP.NET Core / Kestrel) that chats over a WebSocket protocol,
persists state in embedded SQLite, and optionally drives an Unreal Engine avatar. The
Windows-only WPF client (`House/House.ChatDesktop`) is a thin GUI front-end.

Standard commands live in `SoulCore/README.md` (bind/health/WS, build, evidence CLIs).
Notes below are the non-obvious cloud/Linux caveats.

### Build / test / lint (Linux VM)
- Build the backend service: `dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj`
  (or specific projects). This is clean (0 warnings).
- `dotnet build SoulCore/SoulCore.sln` **fails on `House.ChatDesktop` only** with
  `Microsoft.NET.Sdk.WindowsDesktop.targets was not found`. This is expected: it targets
  `net8.0-windows` (WPF) and cannot build/run on Linux. Every other project builds fine.
  Don't try to "fix" this on Linux; it is Windows-only by design.
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
  `SOULCORE_Inference__Model`, `SOULCORE_Hermes__Enabled` (see `SoulCore/.env.example`).
  Secrets can also go in `SoulCore/.env` (gitignored).

### Chat requires an LLM backend (gotcha)
- With defaults (`ChatWs:StubWhenModelDown=false`), a `chat.send` with no reachable LLM
  returns an `error` frame (`chat.model_down`), **not** a reply.
- The default `Inference:Model` in `appsettings.json` is a very large HF GGUF model that is
  not practical to pull in CI. To get real replies, run a local Ollama and override the
  model, e.g. start `ollama serve` (no systemd here — run it manually, e.g. in tmux), pull a
  small model like `qwen2.5:0.5b`, then run the Host with
  `SOULCORE_Inference__Model=qwen2.5:0.5b SOULCORE_Hermes__Enabled=false`.
- To exercise chat wiring without any LLM, set `SOULCORE_ChatWs__StubWhenModelDown=true` for
  deterministic stub replies (`provider=stub`).
- Built-in no-LLM evidence CLIs on the Host: `--emotion-roundtrip`, `--soul-loop-tick
  [--enabled]`, `--secrets-presence`.

### Optional services
- Ollama (`:11434`) or Hermes (`:8642`) for real chat; only one is needed. Ollama needs no
  API key; Hermes chat needs `SOULCORE_HERMES_API_KEY`.
- Unreal Engine avatar bridge (`:8888`) is optional; the Host logs a warning and continues
  when it is unreachable (`unreal.connected=false` in `/health`).
