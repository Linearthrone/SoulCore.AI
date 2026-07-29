#!/usr/bin/env bash
# Start Hermes Agent gateway on loopback :8642 (Linux cloud companion to start-hermes.ps1).
# Does NOT enable SoulCore Hermes.Enabled (BED-144).
set -euo pipefail

PORT="${HERMES_PORT:-8642}"
BIND="${HERMES_BIND:-127.0.0.1}"
SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOULCORE_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"
VENV="${HERMES_VENV:-$SOULCORE_ROOT/.venv-hermes}"
HERMES_HOME="${HERMES_HOME:-$HOME/.hermes}"
PID_FILE="$SCRIPTS_DIR/.hermes.pid"
LOG_FILE="$SCRIPTS_DIR/.hermes.log"
HEALTH="http://${BIND}:${PORT}/health"
HERMES_BIN="$VENV/bin/hermes"

export HERMES_HOME

if curl -fsS -m 2 "$HEALTH" >/dev/null 2>&1; then
  echo "Hermes already up: $(curl -fsS -m 2 "$HEALTH")"
  exit 0
fi

if [[ ! -x "$HERMES_BIN" ]]; then
  echo "ERROR: $HERMES_BIN missing. Recreate:" >&2
  echo "  python3 -m venv $VENV" >&2
  echo "  $VENV/bin/pip install hermes-agent==0.18.2 aiohttp mcp" >&2
  exit 1
fi

if [[ ! -f "$HERMES_HOME/config.yaml" ]]; then
  echo "WARN: missing $HERMES_HOME/config.yaml" >&2
fi
if [[ ! -f "$HERMES_HOME/.env" ]]; then
  echo "WARN: missing $HERMES_HOME/.env (need API_SERVER_* loopback settings)" >&2
fi

if ! curl -fsS -m 2 "http://127.0.0.1:11434/api/tags" >/dev/null 2>&1; then
  echo "WARN: Ollama :11434 unreachable — chat will fail until a model provider is up." >&2
fi

echo "Starting: $HERMES_BIN gateway run (HERMES_HOME=$HERMES_HOME)"
nohup "$HERMES_BIN" gateway run >>"$LOG_FILE" 2>&1 &
echo $! >"$PID_FILE"
echo "PID $(cat "$PID_FILE") -> $PID_FILE"

for _ in $(seq 1 30); do
  if curl -fsS -m 2 "$HEALTH" >/dev/null 2>&1; then
    echo "Health OK: $(curl -fsS -m 2 "$HEALTH")"
    echo "Stop: $SCRIPTS_DIR/stop-hermes.sh   Restart: stop then start"
    exit 0
  fi
  sleep 0.5
done

echo "ERROR: /health did not return within ~15s; see $LOG_FILE and $HERMES_HOME/logs/gateway.log" >&2
exit 1
