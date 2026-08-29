#!/usr/bin/env bash
# Intent / Tasker reachability probe for Termux SMS bridge.
# Writes a line to ~/sms-forward.log — no Host call, no token.
#
# Usage (Tasker Send Intent / Termux:Tasker smoke):
#   sms-ping.sh
#   sms-ping.sh '%SMSRF' '%SMSRB'
#
# Then on tablet:
#   tail -n 20 ~/sms-forward.log

set -euo pipefail

LOG="${SOULCORE_SMS_LOG:-${HOME}/sms-forward.log}"
TS="$(date -Iseconds 2>/dev/null || date '+%Y-%m-%dT%H:%M:%S%z')"
FROM="${1:-}"
BODY="${*:2}"

mkdir -p "$(dirname "$LOG")"
{
  echo "${TS} ping ok"
  echo "  argv_count=$# from=${FROM:-<empty>} body_len=${#BODY}"
  if [[ -n "$BODY" ]]; then
    # Redact body in log — length only (real SMS may be private).
    echo "  body_preview_len=${#BODY}"
  fi
} >>"$LOG"

echo "wrote ${LOG}"
