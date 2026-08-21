# SMS/MMS gateway → Host (PROP-1.2)

Victoria’s phone number is the **Samsung Tab SM-X218U** cellular MDN (not DIGITS).
Host stays on **loopback** (`127.0.0.1:7700`). Reach it from the tablet via **Tailscale serve** (no Funnel).

## Host env (home PC — never commit)

```text
SOULCORE_COMPANION_API_TOKEN=<≥32 random chars>
SOULCORE_Sms__KurtAllowlistE164=+1XXXXXXXXXX
# optional:
# SOULCORE_Sms__VictoriaMdn=+1YYYYYYYYYY
# SOULCORE_Sms__StubWhenModelDown=true
# SOULCORE_Sms__ConversationSessionId=presence-local
```

Restart Host after setting. Enable Tailscale serve as usual (`tailscale-serve-soulcore.ps1` / ALLSTART).

## Inbound contract

`POST /api/companion/v1/messages/inbound`

Headers:

- `Authorization: Bearer <SOULCORE_COMPANION_API_TOKEN>`
- or `X-Api-Key: <token>`
- `Content-Type: application/json`

Body:

```json
{
  "fromE164": "+1XXXXXXXXXX",
  "text": "hey victoria",
  "imageBase64": optional,
  "contentType": "image/jpeg"
}
```

Response (allowlisted):

```json
{
  "ok": true,
  "dropped": false,
  "replyText": "…",
  "mediaId": null,
  "frameId": "…",
  "sessionId": "presence-local"
}
```

Unknown sender → `ok: true, dropped: true` (silent). No tools run on this path.

ChatDesktop (if open on `/ws` with `sessionId=presence-local`) receives:

1. `chat.done` with `role=user`, `channel=sms` (Kurt’s text / photo)
2. `chat.done` with Victoria’s short reply

## Termux smoke (on the tablet)

Replace host MagicDNS / token / Kurt number:

```bash
TOKEN='…'
HOST='https://YOUR-PC.YOUR-TAILNET.ts.net'   # or http://100.x.y.z:7700 if serve TCP
curl -sS -X POST "$HOST/api/companion/v1/messages/inbound" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"fromE164":"+1XXXXXXXXXX","text":"gateway smoke from tablet"}'
```

For a real SMS→Host bridge later (OPS): Termux Tasker / SMS webhook app that POSTs the same JSON when an SMS arrives. PROP-1.3 wires Host→gateway outbound using `replyText`.

## Security

- Empty allowlist = **deny all**
- Do not put MDNs or tokens in git
- Images stored as companion media attachments only — never tool arguments
