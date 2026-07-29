#!/usr/bin/env bash
# Stop Hermes gateway on loopback :8642.
set -euo pipefail

PORT="${HERMES_PORT:-8642}"
BIND="${HERMES_BIND:-127.0.0.1}"
SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_FILE="$SCRIPTS_DIR/.hermes.pid"

kill_pid() {
  local pid="$1"
  if kill -0 "$pid" 2>/dev/null; then
    echo "Stopping Hermes PID $pid"
    kill "$pid" 2>/dev/null || true
    sleep 0.4
    kill -9 "$pid" 2>/dev/null || true
  fi
}

# Prefer pid file
if [[ -f "$PID_FILE" ]]; then
  FILE_PID="$(tr -d '[:space:]' <"$PID_FILE" || true)"
  if [[ -n "${FILE_PID:-}" ]]; then
    kill_pid "$FILE_PID"
  fi
  rm -f "$PID_FILE"
fi

# Also clear any loopback listener on :PORT owned by hermes
if command -v fuser >/dev/null 2>&1; then
  fuser -k "${PORT}/tcp" 2>/dev/null || true
fi

# Fallback: pkill hermes gateway on this host
pkill -f 'hermes gateway run' 2>/dev/null || true

if curl -fsS -m 1 "http://${BIND}:${PORT}/health" >/dev/null 2>&1; then
  echo "WARN: something still answers on :${PORT}" >&2
  exit 1
fi

echo "Hermes stopped (no listener on ${BIND}:${PORT})."
exit 0
