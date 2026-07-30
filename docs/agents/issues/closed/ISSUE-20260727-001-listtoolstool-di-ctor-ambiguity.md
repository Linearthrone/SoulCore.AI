---
type: issue
id: ISSUE-20260727-001
from: QA-01
priority: P0
status: Fixed
resolved: 2026-07-27
gate: QA-130
related: TASK-131, TASK-133, BED-133, QA-130
fix: Host Program.cs registers `AddSingleton<ITool>(sp => new ListToolsTool(sp))` (factory). Keep factory or `[ActivatorUtilitiesConstructor]`; prefer DI resolve regression test on follow-up.
---

# [已修复 2026-07-27] ISSUE-20260727-001: ListToolsTool DI picks test ctor → circular dependency kills all chat

# ISSUE-20260727-001: ListToolsTool DI picks test ctor → circular dependency kills all chat

## Summary

`AddSingleton<ITool, ListToolsTool>()` causes MS.DI to select the test overload `ListToolsTool(IEnumerable<ITool>)` instead of the intended `ListToolsTool(IServiceProvider)`. That recreates the singleton-construction cycle and throws on every `/ws` chat turn.

## Severity

**P0** — all `chat.send` turns fail; Host `/health` still returns ok so the failure is easy to miss.

## Repro (observed 2026-07-27 during QA-130)

1. Start Host via `SoulCore/scripts/start-soulcore.ps1` (PID was 45172).
2. Open `ws://127.0.0.1:7700/ws` and send any `chat.send`.
3. Client receives 0 frames; Host log:

```
fail: Microsoft.AspNetCore.Server.Kestrel[13]
      Connection id "...": An unhandled exception was thrown by the application.
      System.InvalidOperationException: A circular dependency was detected for the service of type 'System.Collections.Generic.IEnumerable<SoulCore.Inference.ITool>'.
      SoulCore.Host.Ws.ChatWebSocketHandler -> SoulCore.Inference.IToolRegistry(SoulCore.Inference.ToolRegistry) -> System.Collections.Generic.IEnumerable<SoulCore.Inference.ITool> -> SoulCore.Inference.ITool(SoulCore.Inference.Tools.System.ListToolsTool) -> System.Collections.Generic.IEnumerable<SoulCore.Inference.ITool>
```

(Note: stack still says `Tools.System.ListToolsTool` in the type display; source lives under `SoulCore.Inference.Tools.Meta`.)

## Root cause

`ListToolsTool` has two public ctors:

- `ListToolsTool(IServiceProvider)` — production / DI (lazy resolve)
- `ListToolsTool(IEnumerable<ITool>)` — unit-test helper

`AddSingleton<ITool, ListToolsTool>()` lets the container pick the greediest resolvable ctor (`IEnumerable<ITool>`), which closes the cycle with `ToolRegistry`.

## Expected fix (DEV/BED)

Prefer one of:

1. Factory registration: `builder.Services.AddSingleton<ITool>(sp => new ListToolsTool(sp));`
2. Or remove/hide the `IEnumerable<ITool>` ctor from DI (internal + `[ActivatorUtilitiesConstructor]` on the IServiceProvider ctor).

## QA-130 unblock

QA-01 applied option (1) in `SoulCore.Host/Program.cs` so the Phase A gate could run. DEV should confirm the permanent pattern and add a regression test that Host can resolve `ChatWebSocketHandler` / `IToolRegistry` without a circular dependency.
