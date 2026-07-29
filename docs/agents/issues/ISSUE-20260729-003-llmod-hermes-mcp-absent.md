---
type: issue
id: ISSUE-20260729-003
severity: P1
status: open
created: 2026-07-29
filed_by: OPS-01
related_task: TASK-143
blocks: BED-144, QA-145, Phase C/D Hermes MCP routing
---

# ISSUE-003 — LLMOD Hermes MCP servers absent on Linux cloud (OPS-143 Blocked)

## Severity

**P1 — Phase F foundation incomplete.** Gateway process can listen on `:8642`, but
the House Victoria MCP inventory (`house_victoria`, `house_victoria_data`,
`computer_use` as LLMOD stdio servers) is not restorable without the Windows
quarry tree. BED-144 / QA-145 / desktop-browser-trading Hermes paths stay blocked
for MCP-backed tools (`mt4_*`, `browser_bridge`, HV memory/task/workflow).

## Summary

OPS-143 searched this Linux cloud workspace for Hermes / LLMOD:

| Path | Result |
| --- | --- |
| `C:\Users\kurtw\LLMOD\LLMOD-max-master` | Not mounted / not present |
| Any `**/LLMOD*/**` under `/workspace` | **0 hits** |
| `MCPServer/house_victoria_mcp/` | **Absent** |
| Pip `hermes-agent==0.18.2` | Restored into `SoulCore/.venv-hermes`; `/health` 200 |

`hermes mcp list` → **No MCP servers configured.** Built-in hermes toolsets expose
a generic `computer_use` checkbox, but **not** LLMOD's `mt4_*` (11),
`browser_bridge`, or `house_victoria` memory/task/workflow tools.

## Impact

- Acceptance criteria 2–3 of TASK-143 cannot Pass on this VM.
- OpenAI `tools` + `tool_choice` requests are accepted, but the gateway ran an
  **agent-side** loop and returned final `content` (no client-visible
  `tool_calls`) in OPS probes — BED-144 must validate passthrough vs agent-wrap
  semantics after MCP is present.
- Phase C/D Hermes backend routing has no MCP targets to call.

## TT-01 avenues (recommended)

1. **Sync quarry MCP trees into the cloud workspace** (read-only copy OK):
   - `LLMOD-max-master/MCPServer/` (esp. `house_victoria_mcp/`)
   - Any `computer_use` / `house_victoria_data` MCP packages referenced by quarry
     `~/.hermes/config.yaml` / `start.ps1`
   - Document exact stdio launch commands from the Windows machine that previously
     returned health `hermes-agent 0.18.2` (QA-005 / OPS-002 era).
2. **Or** mount / rsync the live Windows quarry into the agent environment and
   re-dispatch OPS-143 (or a follow-up OPS ticket) to wire `mcp_servers:` and
   re-run tool-list + smoke.
3. Capture Windows `~/.hermes/config.yaml` MCP block (redact secrets) as the
   canonical wiring template for Linux.

## Workaround (partial)

Gateway-only restore is documented in
`docs/agents/RUNBOOK-Hermes-Gateway-8642.md` + `SoulCore/scripts/start-hermes.*`.
SoulCore may use Ollama tool-loop (native C#) meanwhile; leave
`Hermes.Enabled=false`.

## Close when

1. All three MCP servers register in `hermes mcp list` / Hermes tool listing.
2. Tool families visible: `mt4_*`, `computer_use` (HV), `browser_bridge`,
   `memory_*`, `task_*`, `workflow_*`.
3. OPS re-runs TASK-143 acceptance 2–4 with evidence pasted.
