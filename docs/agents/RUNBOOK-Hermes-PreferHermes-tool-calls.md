# RUNBOOK — Hermes PreferHermes client tool_calls (OPS-163)

**Status (2026-07-29):** **Blocked on Hermes Agent 0.18.2** — no ops knob yields OpenAI client-visible `tool_calls`.

## Scope

SoulCore PreferHermes (BED-161) expects:

`Hermes = LLM-only` → response `message.tool_calls[]` → Host `IToolRegistry` → `CallMcpToolAsync`.

Live gateway on this machine: `%LOCALAPPDATA%\hermes` / `SoulCore/scripts/start-hermes.ps1` → `127.0.0.1:8642`.

## OPS-178 — hide blank MCP `python.exe` consoles

Hermes gateway starts with `-WindowStyle Hidden`, but MCP stdio children from
`%LOCALAPPDATA%\hermes\config.yaml` (`mcp_servers.*.command`) often point at
console-subsystem `…\MCPServer\.venv\Scripts\python.exe`. Those allocate blank
visible consoles that stay open until Hermes stops.

**Durable fix (in-repo):**

| Piece | Role |
| --- | --- |
| `SoulCore/scripts/patch-hermes-mcp-pythonw.ps1` | One-shot / re-apply: rewrite `command:` `python.exe` → `pythonw.exe` when sibling exists; backup `*.bak-ops178-*` |
| `SoulCore/scripts/start-hermes.ps1` | Runs the patch on every start (unless `-SkipMcpPythonwPatch`); ForceRestarts if config changed |
| `ALLSTART.ps1` | Calls `start-hermes.ps1` → inherits the patch |

**After Hermes reinstall / LLMOD `Tools\setup-hermes-integration.ps1`:**

1. Prefer editing quarry setup so MCP `command` writes `pythonw.exe` (not `python.exe`).
2. Or just re-run ALLSTART / `start-hermes.ps1` — preflight rewrites again.
3. Manual: `.\SoulCore\scripts\patch-hermes-mcp-pythonw.ps1` then `.\SoulCore\scripts\start-hermes.ps1 -ForceRestart`

Do **not** kill MCP servers to hide windows (agency regression).

## Knobs checked (none flip client tool_calls)

| Location | Keys / fields | Effect on PreferHermes |
| --- | --- | --- |
| `%LOCALAPPDATA%\hermes\.env` | `API_SERVER_ENABLED`, `API_SERVER_HOST`, `API_SERVER_PORT`, `API_SERVER_KEY`, `API_SERVER_MODEL_NAME` | Bind/auth/model name only |
| `%LOCALAPPDATA%\hermes\config.yaml` | `platforms.api_server.*`, `platform_toolsets.*`, `mcp_servers.*` | Server-side agent tools / MCP inventory |
| Request body | `tools[]`, `tool_choice` | Accepted for fingerprinting; **not** returned as `tool_calls` |
| Capabilities | `runtime.tool_execution` / `split_runtime` | **Hardcoded** in `gateway/platforms/api_server.py` |

No `TOOL_EXECUTION`, `SPLIT_RUNTIME`, or `API_SERVER_TOOL_*` env vars exist in 0.18.2.

## Source contract (0.18.2)

`GET /v1/capabilities` runtime block is literal:

```text
mode: server_agent
tool_execution: server
split_runtime: false
description: "... unless a future explicit split-runtime mode is enabled."
```

Chat Completions non-stream response builds only:

```text
choices[0].message = { role: assistant, content: <final_response> }
finish_reason: stop | length | error
```

Upstream docs: split-runtime “remote brain, local hands” tracked as [NousResearch/hermes-agent#18715](https://github.com/NousResearch/hermes-agent/issues/18715) — **not** shipped in 0.18.2.

## Ops evidence path

`tmpcode/ops163-evidence/` (local, not required in git):

- `01-capabilities-before.json` / `07-capabilities-after.json`
- `04-completions-tool-choice-none.json` — `tools[]` present, response keys `role`+`content` only
- `06-api-server-hardcode-excerpt.txt`

## What OPS will not do

- Patch Hermes Python under `%LOCALAPPDATA%\hermes\hermes-agent` for SoulCore (out of ops config scope).
- Flip `SoulCore.Host` PreferHermes permanently.
- Mark ISSUE-20260729-002 Fixed without proven `tool_calls`.

## Avenue for TT/PM

| Avenue | Owner | Notes |
| --- | --- | --- |
| **A** Wait for Hermes split-runtime / client `tool_execution` release; re-run OPS probe | OPS + upstream | Gate on `#18715` or newer hermes-agent that advertises `tool_execution=client` |
| **B** PreferHermes chat via Ollama tool-loop; Hermes only for `CallMcpToolAsync` MCP | BED | Does not need client tool_calls from Hermes chat |
| **C** Host-side fork/proxy that translates Hermes agent events → OpenAI `tool_calls` | BED/OPS | Custom shim; not stock 0.18.2 |
| **D** Patch local `api_server.py` to passthrough client tools (unsupported) | — | Rejected for house ops; breaks upgrade path |
