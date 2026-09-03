---
issue_id: ISSUE-20260727-007
discovered: 2026-07-27
severity: P1
status: Fixed
fixed: 2026-07-27
fixed_by: TASK-161
task: TASK-145
---
# [已修复 2026-07-27] BED-161: PreferHermes = Host-owned SoulCore ITool loop; Hermes LLM-only.

## Problem Description

With `Hermes.Enabled=true` + `ChatWs.PreferHermes=true`, chat rounds route to Hermes (`chat.done.provider=hermes`), but the Hermes gateway (`tool_execution: server`) runs its **own** agent/tool set. SoulCore registry tools (`browser_capture_tab`, `mt4_status`, `execute_trade`) are not invoked; the model reports them as missing and substitutes Hermes-native tools (e.g. `browser_snapshot`, `computer_use`).

This blocks the Phase F exit gate intent: SoulCore chat → local `ITool` dispatch → `CallMcpToolAsync` / Hermes MCP for house_victoria tools.

## Reproduction Steps

1. Start Hermes `:8642` (`start-hermes.ps1`).
2. Start Host with env: `SOULCORE_Hermes__Enabled=true`, `SOULCORE_ChatWs__PreferHermes=true`, correct `SOULCORE_HERMES_API_KEY` (= Hermes `API_SERVER_KEY`).
3. WS `chat.send`: "You must call the mt4_status tool now..."
4. Observe `provider=hermes` reply claiming `mt4_status` does not exist.
5. Same for `execute_trade` / `browser_capture_tab` (uses `browser_snapshot` instead).

## Expected Result

Model emits SoulCore tool_calls (or content-leak recovery) → Host dispatches `ITool` → Hermes MCP (`mt4_*` / `browser_bridge_*` / `computer_use`).

## Actual Result

Hermes server-side agent ignores SoulCore tool names; no `Hermes tool dispatch` / `Ollama tool dispatch` for those tools on PreferHermes path. Desktop screenshot path did hit Hermes-native `computer_use` (see ISSUE-008).

## Evidence

`tmpcode/qa145-evidence/ac3-browser.json`, `ac4-mt4-status.json`, `ac5a-trade-phase1.json`

## Impact Scope

QA-145 PreferHermes MCP ACs (browser / MT4 read / MT4 trade) Fail. BED-144 `CallMcpToolAsync` path still works when **Ollama** emits SoulCore tool_calls (`PreferHermes=false`).

## Suggested Fix (PM → BED)

- Configure Hermes client-side tool loop (expose OpenAI `tool_calls` to SoulCore), **or**
- Map PreferHermes so SoulCore still owns tool dispatch (Hermes as completion-only / forced `tool_choice` per BED-144), **or**
- Document PreferHermes as Hermes-native tools only and change Phase F AC accordingly.
