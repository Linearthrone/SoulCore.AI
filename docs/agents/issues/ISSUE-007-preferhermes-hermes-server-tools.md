---
type: issue
id: ISSUE-007
severity: P0
status: Fixed
created: 2026-07-27
filed_by: QA-01
fixed_by: BED-01
related_task: TASK-145, TASK-161
gate: QA-145 (PreferHermes Hermes MCP end-to-end)
updated: 2026-07-27 (Fixed by BED-161: PreferHermes Host-owned SoulCore ITool loop; MCP via CallMcpToolAsync)
---

# ISSUE-007 — PreferHermes dispatched Hermes server-side agent tools instead of SoulCore ITools

## Severity

**P0 — Architecture break.** PreferHermes chat turns let the Hermes API server (`tool_execution: "server"`) run its own agent/MCP tools and return final text without Host executing SoulCore `ITool` adapters. Desktop/browser/MT4 gates, confirmations, and `CallMcpToolAsync` wiring were bypassed.

## Summary

QA-145 with `ChatWs.PreferHermes=true` observed Hermes completing tool-bearing turns server-side. SoulCore's `CompleteWithToolsAsync` expected OpenAI `tool_calls` → `IToolRegistry`, but live Hermes often returns no client-visible `tool_calls` and executes MCP/native tools on the gateway host instead.

## Fix (BED-161)

- PreferHermes path: Host-owned SoulCore tool-loop; Hermes is LLM-only for the turn.
- Wire-name aliases (`computer_use` → `desktop_screenshot`, `browser_bridge_*` → `browser_*`) so MCP-shaped calls still hit SoulCore ITools.
- Hermes-backend ITools invoke MCP only via `IHermesMcpInvoker.CallMcpToolAsync` (BED-144).
- PreferHermes fail-fast when gateway/key down — no Ollama fallback (also ISSUE-010).

## Status

**Fixed**
