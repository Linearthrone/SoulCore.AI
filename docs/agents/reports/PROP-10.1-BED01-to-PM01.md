---
type: report
prop_id: PROP-10.1
from: BED-01
to: PM-01
verdict: Pass
branch: cursor/prop10-inference-split-land-9d6e
date: 2026-09-05
landed: 2026-09-14
---

# PROP-10.1 BED-01 → PM-01

## Verdict: **Pass**

`SoulCore.Inference` now has a clear in-project folder boundary: **Clients/** (talk to the model), **Tooling/** (registry + loop helpers), **Tools/**\* (capability tools). No new `SoulCore.Tools` project; no `Program.cs` DI churn beyond existing PROP-9 Hosting modules picking up namespace usings.

## Structure

```
SoulCore.Inference/
├── Clients/          # SoulCore.Inference.Clients
│   ├── IInferenceClient, OllamaInferenceClient, NullInferenceClient
│   ├── IEmbeddingClient, OllamaEmbeddingClient, NullEmbeddingClient
│   ├── IUeLiveSignal, NullUeLiveSignal
│   └── IHermesMcpInvoker (interface retained; no Hermes reintroduction)
├── Tooling/          # SoulCore.Inference.Tooling
│   ├── ITool, IToolRegistry, ToolRegistry, ToolDefinition, ToolResult
│   ├── ChatMessage, IChatSessionHistoryStore, ChatSessionHistoryStore
│   ├── ToolLoopOptions, InferenceModelRouting
│   └── ToolCallTextRecovery (moved from Tools/)
├── Tools/            # SoulCore.Inference.Tools.* (unchanged capability layout)
│   ├── Body/, Browser/, Desktop/, Email/, FS/, System/, Trading/, Workflow/
│   └── RecallMemoryTool, StoreMemoryTool
├── Presence/         # SoulCore.Inference.Presence (unchanged)
└── GlobalUsings.cs   # internal global usings for Clients + Tooling
```

## What changed

| Area | Action |
| --- | --- |
| `SoulCore.Inference` | Git-moved 18 root files → `Clients/` (9) + `Tooling/` (9); namespaces updated |
| `GlobalUsings.cs` | Added for internal cross-folder refs (`ITool` in Tools/*, etc.) |
| `SoulCore.Inference.csproj` | Comment on Memory project ref (Recall/Store tools) |
| Host / Hermes / Protocol.Tests | `using SoulCore.Inference.Clients` + `.Tooling` (minimal; no Program.cs rewrite) |
| `ChatWebSocketHandlerToolLoopTests` | `using` update only — main's PROP-7 alignment kept as-is |
| `Mt4ToolIntentTests` | `ToolResult` → `SoulCore.Inference.Tooling` |

## Evidence

```text
dotnet build SoulCore/SoulCore.Inference/SoulCore.Inference.csproj  → 0 errors
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj            → 0 errors
dotnet build SoulCore/SoulCore.Protocol.Tests/...                   → 0 errors
dotnet test … --filter InferenceModelRoutingTests|ToolRegistryTests|ToolCallTextRecoveryTests|
  OllamaToolLoopTests|OllamaVisionWireTests|ChatWebSocketHandlerToolLoopTests|
  MemoryToolsTests|Mt4ToolIntentTests                               → 111 passed
```

## Fences respected

- No ChatWebSocketHandler structural rewrite
- No SQLite / Memory repo changes
- No Desktop Sleep / drag timing
- No Hermes reintroduction (interface kept; Host path unchanged)
- No `SoulCore.Tools` second project (folders sufficient)

## Notes

- Four pre-existing SMS/filesystem test failures remain outside Inference scope (`SmsSecurityGateTests`, one symlink FS test); core Inference/Protocol suite is green.

## Landing addendum (2026-09-14)

The original PROP-10.1 work was reported Pass but never reached `main`. It has now been
cherry-picked onto current `main` as `8c50fb7` + `7611ea0`.

- The stacked PROP-9.1 Hosting DI extraction (`efa9c03`, `9859969`) was **skipped**: that work
  already reached `main` independently, so replaying it would have duplicated it. `main`'s
  PROP-9 DI layout is authoritative; PROP-10.1 only updates its namespace usings.
- `ChatWebSocketHandler` is a thin facade over `ChatWebSocketSessionRunner` on current `main`
  (PROP-8) and needs no Inference usings, so the original commit's using block does not apply.
- `ChatContextBuilder`, `ChatSendHandler` and their tests postdate the original branch and were
  migrated to `Clients`/`Tooling` as part of landing.
