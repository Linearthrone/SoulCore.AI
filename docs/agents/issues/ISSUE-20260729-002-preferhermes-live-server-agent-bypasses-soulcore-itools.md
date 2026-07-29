---
issue_id: ISSUE-20260729-002
discovered: 2026-07-29
severity: P1
status: Fixed
fixed: 2026-07-29
fixed_by: TASK-164
pm_decision: docs/agents/reports/TASK-20260729-PM01-preferhermes-avenue-b.md
ops_proof: docs/agents/log/TASK-20260729-163-OPS01-to-PM01.md
---

# [已修复 2026-07-29] BED-164 Avenue B: PreferHermes = Ollama tool-loop; Hermes MCP-only

## Problem Description

With PreferHermes routing chat tool-loops through Hermes `CompleteWithToolsAsync` /
`tools[]`, hermes-agent **0.18.2** (`tool_execution: server`) runs its **live
server agent** and does not return client-visible `message.tool_calls`. SoulCore
ITools are bypassed; Hermes-native tools substitute.

Related: ISSUE-20260727-007 (BED-161 Host ITool intent) — regression root cause
is Hermes not exposing client tool_calls (OPS-163).

## Reproduction Steps

1. Hermes `:8642` v0.18.2 up; Host `Hermes.Enabled=true`, `PreferHermes=true`.
2. PreferHermes turn calls Hermes `CompleteWithToolsAsync` with SoulCore `tools[]`.
3. Observe `provider=hermes` / server-agent reply without SoulCore ITool dispatch.

## Expected Result (Avenue B)

PreferHermes → Ollama `CompleteWithToolsAsync` + SoulCore ITool dispatch;
hermes backends → `CallMcpToolAsync` only; Hermes `CompleteWithToolsAsync` never
used for PreferHermes turns.

## Actual Result (before fix)

PreferHermes used Hermes `CompleteWithToolsAsync`; server agent bypassed SoulCore ITools.

## Fix (BED-164)

- `ChatWebSocketHandler.CompleteChatWithToolsAsync`: PreferHermes →
  `EnsureMcpReadyAsync` + Ollama tool-loop; early return (no Hermes tools[]).
- `IHermesClient.EnsureMcpReadyAsync` for MCP gateway/key fail-fast.
- PreferHermes=false path unchanged (Ollama primary; Hermes secondary OK).

## Evidence

- Unit: `PreferHermes_True_RoutesToOllamaToolLoop_HermesMcpOnly`
- Unit: `PreferHermes_HermesMcpDown_FailFast_DoesNotRunOllamaToolLoop`
- Unit: `EnsureMcpReady_GatewayDown_ThrowsUnavailable_FailFast`
- Report: `docs/agents/reports/TASK-20260729-164-BED01-to-PM01.md`
