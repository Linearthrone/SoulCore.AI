# PROP-22.2 — BED-01 → PM-01: operator routes, inference selection, host log tail

**Status:** done (backend). Contract: `docs/agents/unexecuted_proposals/presence-glass-shell.md` §3.1–3.4. Depends on PROP-22.1.

## Files touched

| File | Change |
|---|---|
| `SoulCore/SoulCore.Host/Inference/InferenceRuntimeSelection.cs` | new — thread-safe singleton: `GetEffective(opts)` → `(Model, Cloud, LeisureModel, BaseUrlHint)`, `Apply(model, cloud?)`, `Clear()` |
| `SoulCore/SoulCore.Host/Inference/InferenceRuntimeRouteHandler.cs` | new — `DelegatingHandler` on the `OllamaInferenceClient` typed HttpClient; re-points requests to ollama.com / local `:11434` when the cloud override differs from configured `BaseUrl` (key resolved at call time, never logged) |
| `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/InferenceServiceCollectionExtensions.cs` | registers `InferenceRuntimeSelection` singleton + route handler; `.AddHttpMessageHandler<…>()` |
| `SoulCore/SoulCore.Host/Ws/ChatSendHandler.cs` | optional ctor param `InferenceRuntimeSelection?`; `ApplyRuntimeModel()` sets `ToolLoopOptions.ModelOverride` for normal chat tool-loop turns (keeps `ForceToolName`). Leisure model untouched |
| `SoulCore/SoulCore.Host/Hosting/OperatorSurfaceApiExtensions.cs` | new — all new routes (`MapOperatorSurfaceApi`) |
| `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` | calls `app.MapOperatorSurfaceApi()` in `UseSoulCoreWeb`; `CompanionEmailAuthFilter` made `internal`; `/health` `inference.model/cloud/provider/baseUrl` now from `GetEffective` (+ `leisureModel`) |

> The routes live in a new file (called from `UseSoulCoreWeb`) rather than inline, because `WebApplicationExtensions.cs` is already >1,400 lines.

## Routes (all behind `CompanionEmailAuthFilter` — open when no companion token configured)

Bodies accept the contract's snake_case column names (camelCase also accepted); responses use the column names verbatim.

```
GET    /operator/work-items            -> { count, items:[{id,kind,title,description,details,deadline_utc,priority,no_checkin,
                                            phase_checkpoints_json,deliverables_json,needs_extra_permissions,
                                            outside_collaboration,status,created_at,updated_at}] }
POST   /operator/work-items            body {kind(required task|project),title(required),description?,details?,deadline_utc?,priority?(1-5,def 3),
                                            no_checkin?,phase_checkpoints_json?,deliverables_json?,needs_extra_permissions?,
                                            outside_collaboration?,status?}  -> { id, item }   (400 on bad input)
DELETE /operator/work-items/{id}       -> { deleted: id } | 404
GET    /operator/agenda                -> { items:[{id,title,weekday,time_local,notes,created_at}] }
POST   /operator/agenda                body {title,weekday(0-6),time_local?(HH:mm, def 09:00),notes?} -> { id, item }
DELETE /operator/agenda/{id}           -> { deleted: id } | 404
GET    /settings/inference/profiles    -> { active:{model,cloud,leisureModel}, profiles:[{id,label,model,cloud}] }
POST   /settings/inference/apply       body {model, cloud?} -> { ok, active }
GET    /settings/system-log?lines=200  -> { available, path?, lines:[] }   (lines clamped 1..2000)
```

Profiles = configured `Model` (cloud per `BaseUrl`), `LeisureModel` (cloud:false), plus each tag from `http://127.0.0.1:11434/api/tags` (cloud:false, 2 s timeout, silently skipped if unreachable). Deduped on model+cloud.

System log: tails `SoulCore/scripts/.soulcore-host.log` (found by walking up from app base dir / cwd). Opened with `FileShare.ReadWrite` (Host keeps writing it). Missing/unreadable → `available:false, lines:[]`. Only real lines returned; obvious credentials (`Bearer …`, `api_key=…`, `token/secret/password=…`) are masked.

## Evidence (isolated Host copy on :7799, temp DB; live :7710 host not touched)

```
GET /operator/work-items -> 200 {"count":0,"items":[]}
POST /operator/work-items {"kind":"project","title":"Ship glass shell",...} -> 200 {"id":1,"item":{"id":1,"kind":"project","title":"Ship glass shell","deadline_utc":"2026-12-01T12:00:00.000Z","priority":4,"no_checkin":1,"phase_checkpoints_json":"[\"design\",\"build\"]",...,"status":"open"}}
POST /operator/work-items {"kind":"bogus","title":"x"} -> 400
DELETE /operator/work-items/2 -> 200 {"deleted":2}     DELETE /operator/work-items/999 -> 404
POST /operator/agenda {"title":"Standup","weekday":0,"time_local":"09:30","notes":"daily"} -> 200 {"id":1,"item":{...}}
POST /operator/agenda weekday=9 -> 400                 POST /operator/agenda time_local="9:5" -> 400
GET /settings/inference/profiles -> {"active":{"model":"gemma4:latest","cloud":false,"leisureModel":"qwen2.5:1.5b"},"profiles":[{"id":"configured",...},{"id":"leisure",...}]}   (Ollama not running -> no tag profiles)
POST /settings/inference/apply {"model":"qwen2.5:7b","cloud":true} -> {"ok":true,"active":{"model":"qwen2.5:7b","cloud":true,"leisureModel":"qwen2.5:1.5b"}}
POST /settings/inference/apply {"cloud":true} -> 400
GET /health .inference -> before: model=gemma4:latest cloud=false provider=ollama baseUrl=loopback
                          after apply: model=qwen2.5:7b cloud=true provider=ollama-cloud baseUrl=https://ollama.com leisureModel=qwen2.5:1.5b
GET /settings/system-log (repo layout, no log found) -> {"available":false,"lines":[]}
GET /settings/system-log?lines=3 (fixture log, 301 lines) -> {"available":true,"path":"...\\scripts\\.soulcore-host.log","lines":["line 299 ok","line 300 ok","auth Bearer *** and api_key=*** end"]}
lines default=200, lines=100000 -> capped at file contents (301 < 2000)
```

Builds:

```
dotnet build SoulCore/SoulCore.Memory/SoulCore.Memory.csproj   -> 0 Warning(s) 0 Error(s)
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj       -> 0 Warning(s) 0 Error(s)
dotnet build SoulCore/SoulCore.Protocol.Tests                  -> 0 Error(s)  (ChatSendHandler ctor param is optional)
```

## Caveats / not verified

- **Live model switch against a real model was not exercised** (Ollama and cloud key absent here). Verified: selection state, `/health` reflection, build. The wiring: tool-loop turns get `ModelOverride`; cloud flips are applied by `InferenceRuntimeRouteHandler` on the outgoing request.
- Single-shot `IInferenceClient.CompleteAsync` (used by `CompleteChatAsync` fallback) has no per-call model parameter, so it still uses `InferenceOptions.Model`; the normal chat path is the tool loop and is covered.
- Selection is in-memory only: resets on Host restart. `apply` never writes config/secrets.
- Companion-token rejection path (401) reuses the existing `CompanionEmailAuthFilter`; not re-tested with a token configured.
- `DELETE` returns 404 for unknown ids.
