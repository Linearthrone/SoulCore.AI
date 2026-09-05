---
type: report
prop_id: PROP-9.1
from: BED-01
to: PM-01
verdict: Pass
branch: cursor/prop9-di-modules-8a1f
date: 2026-09-05
---

# PROP-9.1 BED-01 → PM-01

## Verdict: **Pass**

Program.cs DI registrations extracted into auditable `Hosting/` composition modules. Behavior parity preserved — pure move/refactor, no feature hitchhikers.

## What changed

| Area | Action |
| --- | --- |
| `Program.cs` | Reduced to config bind, SEC-004 bind gate, ordered `Add*` calls, bootstrap (WS adapter start, optional Unreal connect, `RunAsync`) |
| `Hosting/ServiceCollectionExtensions/` | New modules: `AddMemory`, `AddInference`, `AddTools`, `AddCompanion`, `AddVoice` |
| `Hosting/WebApplicationExtensions.cs` | `UseSoulCoreWeb` — WebSockets middleware, WS handler, companion/voice APIs, `/health`, settings, desktop/browser view endpoints |
| `Hosting/OllamaHttpClientConfiguration.cs` | Shared Ollama HTTP client helper (extracted from Program.cs) |

## Module file list

```
SoulCore/SoulCore.Host/Hosting/
├── OllamaHttpClientConfiguration.cs
├── WebApplicationExtensions.cs          # UseSoulCoreWeb (AddWebEndpoints equivalent)
└── ServiceCollectionExtensions/
    ├── MemoryServiceCollectionExtensions.cs    # AddMemory
    ├── InferenceServiceCollectionExtensions.cs # AddInference
    ├── ToolsServiceCollectionExtensions.cs     # AddTools
    ├── CompanionServiceCollectionExtensions.cs # AddCompanion
    └── VoiceServiceCollectionExtensions.cs   # AddVoice
```

## Composition order (unchanged semantics)

```csharp
builder.Services
    .AddMemory(memoryOptions, safetyOptions)
    .AddInference(inferenceOptions)
    .AddTools()
    .AddCompanion(unrealOptions)
    .AddVoice(voiceOptions);

var app = builder.Build();
app.UseSoulCoreWeb(chatWsOptions);
```

## Evidence

```text
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj  → 0 errors (1 pre-existing CA1416 VoiceSpeakService platform warning)
curl http://127.0.0.1:7700/health                        → {"status":"ok","service":"SoulCore.Host",...}
Program.cs                                               → ~220 lines (was ~1188)
```

## Fences respected

- No ChatWebSocketHandler structural split (PROP-8)
- No Inference folder moves (PROP-10)
- No feature hitchhikers

## Notes

- `/health` now resolves `IOptions<InferenceOptions>` inside the handler instead of closing over bootstrap locals — same JSON shape.
- `VoiceSpeakService` CA1416 warning pre-existed (Windows-only TTS path).
