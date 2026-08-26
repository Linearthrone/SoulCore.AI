#!/usr/bin/env bash
# Termux SMS -> SoulCore Host inbound bridge (PROP-1.2).
# Never embed or print the companion token.
#
# Usage:
#   sms-to-victoria.sh <from> <body...>
#   sms-to-victoria.sh --from '+15551234567' --text 'hello'
#   sms-to-victoria.sh --health
#
# Token file (tablet): ~/.config/soulcore/companion.token  (one line, chmod 600)
# Host default: Tailscale TCP serve. Override with SOULCORE_HOST or --host.

set -euo pipefail

HOST="${SOULCORE_HOST:-http://100.71.223.95:7700}"
TOKEN_FILE="${SOULCORE_TOKEN_FILE:-${HOME}/.config/soulcore/companion.token}"
FROM=""
TEXT=""
DO_HEALTH=0

usage() {
  cat <<'EOF'
sms-to-victoria.sh — POST an inbound SMS to SoulCore Host

  sms-to-victoria.sh <from> <body...>
  sms-to-victoria.sh --from E164 --text BODY
  sms-to-victoria.sh --health

Env:
  SOULCORE_HOST         default http://100.71.223.95:7700
                        (HTTPS: https://kaia-reimagined.tailbf9ec2.ts.net:8443)
  SOULCORE_TOKEN_FILE   default ~/.config/soulcore/companion.token

The token file must exist on the tablet. Do not put the token in this script.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    --health)
      DO_HEALTH=1
      shift
      ;;
    --host)
      HOST="${2:?--host requires a URL}"
      shift 2
      ;;
    --token-file)
      TOKEN_FILE="${2:?--token-file requires a path}"
      shift 2
      ;;
    --from)
      FROM="${2:?--from requires an E.164 sender}"
      shift 2
      ;;
    --text)
      TEXT="${2:?--text requires a message body}"
      shift 2
      ;;
    --)
      shift
      break
      ;;
    -*)
      echo "unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
    *)
      break
      ;;
  esac
done

HOST="${HOST%/}"

if [[ "$DO_HEALTH" -eq 1 ]]; then
  echo "GET ${HOST}/health"
  curl -sS --max-time 15 "${HOST}/health"
  echo
  exit 0
fi

if [[ -z "$FROM" && $# -ge 1 ]]; then
  FROM="$1"
  shift
fi
if [[ -z "$TEXT" ]]; then
  if [[ $# -gt 0 ]]; then
    TEXT="$*"
  fi
fi

if [[ -z "$FROM" ]]; then
  echo "missing sender. Usage: sms-to-victoria.sh <from> <body...>" >&2
  exit 2
fi

# Host also normalizes; keep Tasker formats (+1… / 1… / 10-digit) usable.
FROM="$(printf '%s' "$FROM" | tr -d ' ()-')"
case "$FROM" in
  +*) ;;
  1[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]) FROM="+${FROM}" ;;
  [0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]) FROM="+1${FROM}" ;;
  *)
    if [[ "$FROM" =~ ^[0-9]+$ ]]; then
      FROM="+${FROM}"
    fi
    ;;
esac

if [[ ! -f "$TOKEN_FILE" ]]; then
  echo "token file missing: $TOKEN_FILE" >&2
  echo "create it on the tablet (one line, chmod 600). Do not put the token in this script." >&2
  exit 2
fi

TOKEN="$(tr -d '\r\n' <"$TOKEN_FILE")"
if [[ -z "$TOKEN" ]]; then
  echo "token file empty: $TOKEN_FILE" >&2
  exit 2
fi
TOKEN_LEN="${#TOKEN}"
echo "host=${HOST} tokenPresent=true tokenLen=${TOKEN_LEN} from=${FROM} textLen=${#TEXT}"

json_body() {
  if command -v jq >/dev/null 2>&1; then
    jq -n --arg from "$FROM" --arg text "$TEXT" '{fromE164:$from, text:$text}'
    return
  fi
  if command -v python3 >/dev/null 2>&1; then
    python3 -c 'import json,sys; print(json.dumps({"fromE164":sys.argv[1],"text":sys.argv[2]}))' "$FROM" "$TEXT"
    return
  fi
  echo "need jq or python3 to JSON-encode the SMS body (pkg install jq)" >&2
  exit 2
}

body_file="$(mktemp)"
out_file="$(mktemp)"
trap 'rm -f "$body_file" "$out_file"' EXIT
json_body >"$body_file"

code="$(
  curl -sS --max-time 60 -o "$out_file" -w '%{http_code}' \
    -X POST "${HOST}/api/companion/v1/messages/inbound" \
    -H "Content-Type: application/json; charset=utf-8" \
    -H "X-Api-Key: ${TOKEN}" \
    --data-binary "@${body_file}"
)"
echo "HTTP ${code}"
if [[ -s "$out_file" ]]; then
  cat "$out_file"
  echo
fi
case "$code" in
  200) exit 0 ;;
  *) exit 1 ;;
esac
