#!/usr/bin/env bash
# PROP-5.4 concurrent soak: /health probes while Protocol.Tests soak runs.
# Linux/cloud-friendly companion to soak-soulcore.ps1 (health-only slice).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:7700/health}"
PROBES="${PROBES:-30}"
INTERVAL_SEC="${INTERVAL_SEC:-1}"
LOG_DIR="$ROOT/scripts/logs"
STAMP="$(date +%Y%m%d-%H%M%S)"
LOG_FILE="$LOG_DIR/prop54-soak-$STAMP.log"

mkdir -p "$LOG_DIR"

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

health_ok=0
health_fail=0
fail_streak=0
max_fail_streak=0
abort_reason=""

log "=== PROP-5.4 concurrent soak start ==="
log "HealthUrl=$HEALTH_URL Probes=$PROBES IntervalSec=$INTERVAL_SEC"
log "LogFile=$LOG_FILE"

# Run unit soak in background while probing /health.
(
  cd "$ROOT"
  dotnet test SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
    --no-restore \
    --filter "FullyQualifiedName~Prop54|FullyQualifiedName~SqlitePathGate" \
    --verbosity minimal
) >"$LOG_DIR/prop54-test-$STAMP.log" 2>&1 &
TEST_PID=$!
log "Started dotnet test pid=$TEST_PID"

for ((i = 1; i <= PROBES; i++)); do
  code="$(curl -s -o /tmp/prop54-health.json -w '%{http_code}' "$HEALTH_URL" || echo 000)"
  if [[ "$code" == "200" ]]; then
    health_ok=$((health_ok + 1))
    fail_streak=0
    status="$(python3 -c "import json; print(json.load(open('/tmp/prop54-health.json')).get('status','?'))" 2>/dev/null || echo '?')"
    mem_open="$(python3 -c "import json; d=json.load(open('/tmp/prop54-health.json')); print(d.get('memory',{}).get('open','?'))" 2>/dev/null || echo '?')"
    log "PROBE $i OK status=$status memOpen=$mem_open"
  else
    health_fail=$((health_fail + 1))
    fail_streak=$((fail_streak + 1))
    if (( fail_streak > max_fail_streak )); then max_fail_streak=$fail_streak; fi
    log "PROBE $i FAIL http=$code streak=$fail_streak"
    if (( fail_streak >= 3 )); then
      abort_reason="health_fail_streak=$fail_streak"
      break
    fi
  fi
  sleep "$INTERVAL_SEC"
done

if ! wait "$TEST_PID"; then
  log "ABORT: dotnet test failed (see $LOG_DIR/prop54-test-$STAMP.log)"
  abort_reason="${abort_reason:-dotnet_test_failed}"
fi

log "=== PROP-5.4 concurrent soak end ==="
log "HealthOk=$health_ok HealthFail=$health_fail MaxFailStreak=$max_fail_streak"
if [[ -n "$abort_reason" ]]; then
  log "AbortReason=$abort_reason"
  log "SUMMARY {\"pass\":false,\"abortReason\":\"$abort_reason\"}"
  exit 1
fi

log "SUMMARY {\"pass\":true,\"healthOk\":$health_ok,\"healthFail\":$health_fail,\"testLog\":\"$LOG_DIR/prop54-test-$STAMP.log\"}"
exit 0
