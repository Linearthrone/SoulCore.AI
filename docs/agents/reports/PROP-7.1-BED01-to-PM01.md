---
type: report
prop_id: PROP-7-hermes-dead-surface-cleanup
from: BED-01
to: PM-01
verdict: Pass
branch: cursor/prop7-hermes-cleanup-8a1f
date: 2026-09-05
---

# PROP-7.1 BED-01 → PM-01

## Verdict: **Pass**

Hermes live surface removed from Host chat path. Ollama is the unambiguous inference boundary.

## What changed

| Area | Action |
| --- | --- |
| `Program.cs` | Removed `IHermesClient` DI, `HermesOptions` bind/PostConfigure, BED-185 remap block, `BackendHermes` factory arms, `/health` hermes stanza |
| `ChatWebSocketHandler.cs` | DELETE-ONLY: dropped `IHermesClient`/`HermesOptions` ctor params and PreferHermes warning arms (no structural split) |
| `HermesOptions.cs` | Deleted |
| `HermesToolRouting.cs` | Deleted |
| `ChatWsOptions` | Removed `PreferHermes` |
| `ToolsOptions` | Removed `BackendHermes` constant |
| `appsettings*.json` | Removed `Hermes` section and `PreferHermes` |
| Host + Protocol.Tests csproj | Dropped `SoulCore.Hermes` project reference |
| `ChatWebSocketHandlerToolLoopTests` | No Hermes doubles; PreferHermes-specific tests removed |
| Docs | `PRODUCT_ROOT`, handbook inference-tools / overview / modules — Hermes honesty |

## Evidence

```text
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj  → 0 errors
dotnet test … --filter ChatWebSocketHandlerToolLoop       → 19 passed
rg IHermesClient|PreferHermes|HermesOptions|BackendHermes SoulCore/SoulCore.Host SoulCore/SoulCore.Config
  → clean (SecretNames.HermesApiKey in --secrets-presence only)
```

## Fences respected

- No SqliteMemoryStore repo split (PROP-11)
- No ChatWebSocketHandler structural split (PROP-8)
- No Desktop drag / Program.cs DI module extraction beyond Hermes removal

## Notes

- `SoulCore.Hermes` package remains in repo (archived); Host no longer references it.
- Legacy env `SOULCORE_HERMES_API_KEY` still listed in `--secrets-presence` for ops visibility.
