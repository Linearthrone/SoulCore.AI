---
type: runbook
id: RUNBOOK-Hermes-Gateway-8642
from: OPS-01
related_task: TASK-143
created: 2026-07-29
env: Linux cloud + Windows quarry
---

# RUNBOOK — Hermes gateway (`:8642`) restore / start / stop

## What this is

`hermes-agent` **0.18.2** (Nous Research) exposes an OpenAI-compatible HTTP API on
`http://127.0.0.1:8642` (`GET /health`, `POST /v1/chat/completions`). SoulCore's
`HermesHttpClient` talks to this URL when `Hermes.Enabled=true` (**BED-144** —
do **not** flip that flag from OPS).

## Source paths found (OPS-143 Linux cloud)

| Expected (Windows quarry) | Present on this VM? |
| --- | --- |
| `C:\Users\kurtw\LLMOD\LLMOD-max-master` | **No** — entire LLMOD tree absent |
| `LLMOD/.../Hermes/` / `HermesGatewayService` | **No** |
| `LLMOD/MCPServer/house_victoria_mcp/` | **No** |
| `LLMOD` MCP: `house_victoria`, `house_victoria_data`, `computer_use` | **No** |
| Pip package `hermes-agent==0.18.2` | **Yes** — installed into `SoulCore/.venv-hermes` |
| Runtime config `~/.hermes/config.yaml` + `~/.hermes/.env` | **Yes** (local, gitignored secrets) |

Historical live evidence (Windows quarry QA/OPS logs): health body
`{"status":"ok","platform":"hermes-agent","version":"0.18.2"}`.

## Install (Linux)

```bash
python3 -m venv SoulCore/.venv-hermes
SoulCore/.venv-hermes/bin/pip install -r SoulCore/scripts/requirements-hermes.txt
# aiohttp is required for the api_server adapter; mcp for hermes mcp CLI.
```

Bootstrap `~/.hermes`:

```bash
export HERMES_HOME="$HOME/.hermes"
mkdir -p "$HERMES_HOME"
# config.yaml — point model at local Ollama (example for cloud CI-sized model):
#   model.provider: custom
#   model.base_url: http://127.0.0.1:11434/v1
#   model.default: <model with >=64k ctx OR Modelfile override>
#   model.context_length: 65536
# .env (never commit):
#   API_SERVER_ENABLED=true
#   API_SERVER_HOST=127.0.0.1
#   API_SERVER_PORT=8642
#   API_SERVER_KEY=<secret>
#   API_SERVER_MODEL_NAME=local
#   OPENAI_API_KEY=ollama   # placeholder for Ollama OpenAI-compat
```

**Context window gotcha:** hermes-agent 0.18.2 requires ≥64k context. Small Ollama
models (e.g. `qwen2.5:0.5b` at 32k) fail agent init unless you create a Modelfile
override (`PARAMETER num_ctx 65536`) or set `model.context_length` to the true
window (must still be ≥64k per Hermes).

## Start / stop / restart

| OS | Start | Stop |
| --- | --- | --- |
| Windows / pwsh | `SoulCore/scripts/start-hermes.ps1` | `SoulCore/scripts/stop-hermes.ps1` |
| Linux | `SoulCore/scripts/start-hermes.sh` | `SoulCore/scripts/stop-hermes.sh` |

Artifacts: `SoulCore/scripts/.hermes.pid`, `SoulCore/scripts/.hermes.log`
(gitignored). Gateway also logs under `~/.hermes/logs/gateway.log`.

Restart = stop then start. If flaky, check `~/.hermes/logs/errors.log` for
missing `aiohttp`, context-window errors, or SIGTERM from parent shells.

## Health

```bash
curl -sS http://127.0.0.1:8642/health
# expect: {"status": "ok", "platform": "hermes-agent", "version": "0.18.2"}
```

Chat (API key from `~/.hermes/.env` → also set `SOULCORE_HERMES_API_KEY` for Host):

```bash
curl -sS -H "Authorization: Bearer $API_SERVER_KEY" -H 'Content-Type: application/json' \
  http://127.0.0.1:8642/v1/chat/completions \
  -d '{"model":"local","messages":[{"role":"user","content":"ping"}],"max_tokens":32}'
```

## MCP servers (House Victoria) — blocked until quarry sync

`hermes mcp list` shows **no** configured MCP servers on the Linux cloud VM.
Built-in hermes toolsets include a generic `computer_use` family, but **not** the
LLMOD inventory (`mt4_*`, `browser_bridge`, `house_victoria` memory/task/workflow).

Wire after TT-01 ships quarry trees (stdio example):

```yaml
# ~/.hermes/config.yaml
mcp_servers:
  house_victoria:
    command: python
    args: ["-m", "house_victoria_mcp"]   # exact module path after sync
  house_victoria_data:
    command: python
    args: ["-m", "house_victoria_data"]
  computer_use:
    command: python
    args: ["-m", "computer_use"]         # LLMOD MCP, distinct from hermes built-in
```

Then: `hermes mcp test <name>` and restart gateway.

## SoulCore

Keep `Hermes.Enabled=false` in `appsettings.json` until BED-144 + QA-145.
