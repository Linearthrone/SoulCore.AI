#!/usr/bin/env bash
# PROP-1.3: poll Host outbound SMS/MMS queue and send via Termux.
# Never embed the companion token — use ~/.config/soulcore/companion.token
#
# Usage:
#   sms-outbound-poll.sh            # one shot
#   sms-outbound-poll.sh --loop 15  # every 15s
#
# Requires: curl, jq; Termux:API termux-sms-send for SMS.
# MMS: writes JPEG/PNG under ~/storage/downloads/soulcore-mms/ and logs the path
# (Samsung Messages / Tasker can attach; termux-sms-send is SMS-only).

set -euo pipefail

HOST="${SOULCORE_HOST:-https://kaia-reimagined.tailbf9ec2.ts.net:8443}"
HOST="${HOST%/}"
TOKEN_FILE="${SOULCORE_TOKEN_FILE:-${HOME}/.config/soulcore/companion.token}"
LOOP_SECS=0
LOG="${SOULCORE_SMS_LOG:-${HOME}/sms-forward.log}"
MMS_DIR="${HOME}/storage/downloads/soulcore-mms"

usage() {
  cat <<'EOF'
sms-outbound-poll.sh — drain Host outbound SMS/MMS queue

  sms-outbound-poll.sh
  sms-outbound-poll.sh --loop SECONDS
  sms-outbound-poll.sh --host URL

Env:
  SOULCORE_HOST         default https://kaia-reimagined.tailbf9ec2.ts.net:8443
  SOULCORE_TOKEN_FILE   default ~/.config/soulcore/companion.token
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --loop) LOOP_SECS="${2:?}"; shift 2 ;;
    --host) HOST="${2:?}"; HOST="${HOST%/}"; shift 2 ;;
    *) echo "unknown: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ ! -f "$TOKEN_FILE" ]]; then
  echo "token file missing: $TOKEN_FILE" >&2
  exit 2
fi
TOKEN="$(tr -d '\r\n' <"$TOKEN_FILE")"
if [[ -z "$TOKEN" ]]; then
  echo "token file empty" >&2
  exit 2
fi

ack_job() {
  local id="$1" ok="$2" err="${3:-}"
  local body
  body="$(jq -n --argjson ok "$ok" --arg err "$err" '{ok:$ok, error:(if $err=="" then null else $err end)}')"
  curl -sS --max-time 30 -X POST \
    -H "Content-Type: application/json" \
    -H "X-Api-Key: ${TOKEN}" \
    --data-binary "$body" \
    "${HOST}/api/companion/v1/sms/outbound/${id}/ack" >/dev/null || true
}

process_once() {
  local raw jobs_len i id kind to text b64 ct path code
  raw="$(
    curl -sS --max-time 30 -w '\n%{http_code}' \
      -H "X-Api-Key: ${TOKEN}" \
      "${HOST}/api/companion/v1/sms/outbound/pending?limit=5"
  )" || {
    echo "poll curl failed host=${HOST}" >&2
    return 1
  }
  code="$(echo "$raw" | tail -n 1)"
  raw="$(echo "$raw" | sed '$d')"

  if [[ "$code" != "200" ]]; then
    echo "poll HTTP ${code} body=${raw:0:200}" >&2
    return 1
  fi

  if ! echo "$raw" | jq -e '.ok == true' >/dev/null 2>&1; then
    echo "poll bad JSON: ${raw:0:200}" >&2
    return 1
  fi

  jobs_len="$(echo "$raw" | jq '.jobs | length')"
  echo "poll ok jobs=${jobs_len}"
  if [[ "$jobs_len" -eq 0 ]]; then
    return 0
  fi

  mkdir -p "$(dirname "$LOG")" "$MMS_DIR" 2>/dev/null || true
  TS="$(date -Iseconds 2>/dev/null || date '+%Y-%m-%dT%H:%M:%S%z')"

  for ((i = 0; i < jobs_len; i++)); do
    id="$(echo "$raw" | jq -r ".jobs[$i].id")"
    kind="$(echo "$raw" | jq -r ".jobs[$i].kind")"
    to="$(echo "$raw" | jq -r ".jobs[$i].toE164")"
    text="$(echo "$raw" | jq -r ".jobs[$i].text // \"\"")"
    echo "${TS} outbound claim id=${id} kind=${kind} to=${to} textLen=${#text}" >>"$LOG" || true
    echo "sending kind=${kind} to=${to} textLen=${#text}"

    if [[ "$kind" == "sms" ]]; then
      if ! command -v termux-sms-send >/dev/null 2>&1; then
        echo "termux-sms-send missing — pkg install termux-api + install Termux:API Android app" >&2
        ack_job "$id" false "termux-sms-send_missing"
        continue
      fi
      if termux-sms-send -n "$to" "$text"; then
        ack_job "$id" true
        echo "sent sms id=${id}"
        echo "${TS} outbound sms sent id=${id}" >>"$LOG" || true
      else
        echo "termux-sms-send failed id=${id}" >&2
        ack_job "$id" false "termux-sms-send_failed"
      fi
      continue
    fi

    if [[ "$kind" == "mms" ]]; then
      b64="$(echo "$raw" | jq -r ".jobs[$i].imageBase64 // empty")"
      ct="$(echo "$raw" | jq -r ".jobs[$i].contentType // \"image/jpeg\"")"
      if [[ -z "$b64" ]]; then
        ack_job "$id" false "no_image"
        continue
      fi
      ext="jpg"
      case "$ct" in
        image/png) ext="png" ;;
        image/webp) ext="webp" ;;
        image/bmp) ext="bmp" ;;
      esac
      path="${MMS_DIR}/${id}.${ext}"
      echo "$b64" | base64 -d >"$path"
      if [[ -n "$text" ]] && command -v termux-sms-send >/dev/null 2>&1; then
        termux-sms-send -n "$to" "${text} [still saved: ${path}]" || true
      fi
      echo "mms saved path=${path} id=${id}"
      echo "${TS} outbound mms saved path=${path} id=${id}" >>"$LOG" || true
      if command -v termux-notification >/dev/null 2>&1; then
        termux-notification -t "Victoria MMS still" -c "Saved ${path} — attach/send to Kurt in Messages" || true
      fi
      ack_job "$id" true
      continue
    fi

    ack_job "$id" false "unknown_kind"
  done
}

if [[ "$LOOP_SECS" -gt 0 ]]; then
  while true; do
    process_once || true
    sleep "$LOOP_SECS"
  done
else
  process_once
fi
