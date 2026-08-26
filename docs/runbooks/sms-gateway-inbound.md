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

## Auth / 401 with a “perfect” long token

Same gate as `/ws`: `Authorization: Bearer` or `X-Api-Key`.

Windows often has a **stale User/Machine** `SOULCORE_COMPANION_API_TOKEN` that every PowerShell inherits. Older Host/start scripts kept that value and ignored `.env` when the process already had a token → curl (`.env` or a pasted secret) and Host (stale env) disagree → 401 forever even at length 60+.

Verify both sides without pasting the secret:

```powershell
# Poison? (User-level env — often the culprit)
[Environment]::GetEnvironmentVariable("SOULCORE_COMPANION_API_TOKEN","User")

# .env on disk (length only)
(Select-String -Path .\SoulCore\.env -Pattern '^SOULCORE_COMPANION_API_TOKEN=').Line.Split('=',2)[1].Trim().Trim('"').Length

# What Host will use after .env load (length + short fingerprint)
dotnet run --project SoulCore/SoulCore.Host -c Release -- --secrets-presence
```

If User env is set and disagrees with `.env`, clear it once:

```powershell
[Environment]::SetEnvironmentVariable("SOULCORE_COMPANION_API_TOKEN", $null, "User")
```

Then `.\ALLSTART.ps1 -RestartHost` and curl reading the token **from `.env`**, not `$env:`.

**PowerShell JSON footgun:** do **not** put JSON inline in `--data-raw '...'` if you ever see empty **500** / `invalid JSON body`. PS can send empty or UTF-16 bodies. Write UTF-8 to a temp file and use `--data-binary`:

```powershell
$line = (Select-String -Path .\SoulCore\.env -Pattern '^SOULCORE_COMPANION_API_TOKEN=').Line
$token = $line.Substring($line.IndexOf('=') + 1).Trim().Trim('"')
$bodyPath = Join-Path $env:TEMP "soulcore-inbound.json"
[System.IO.File]::WriteAllText($bodyPath, '{"fromE164":"+1XXXXXXXXXX","text":"hey"}', [System.Text.UTF8Encoding]::new($false))
curl.exe -sS -i -X POST "http://127.0.0.1:7700/api/companion/v1/messages/inbound" `
  -H "Content-Type: application/json; charset=utf-8" `
  -H "X-Api-Key: $token" `
  --data-binary "@$bodyPath"
```

Expect:

- **200** `ok:true` — allowlisted + model/stub reply
- **200** `dropped:true` — number not on allowlist
- **503** `chat.model_down` — Ollama down and stub off → set `SOULCORE_Sms__StubWhenModelDown=true` for smoke, or start Ollama
- **400** `invalid JSON body` — body still mangled (never empty 500 after the inbound error-handling fix)

`fp=` from `--secrets-presence` must match a fingerprint of that same `.env` value. Length alone is not enough (two different 64-char tokens both “look fine”).

### ChatDesktop: Host “up” but chat says host down

`/health` is unauthenticated → Services can show Host **up**. Chat uses `/ws`, which requires the companion Bearer. If ChatDesktop has a missing/stale token, WS never connects and send says host down.

Restart ChatDesktop after pulling the `.env`-wins fix (`start-desktopgui.ps1` / `CompanionToken`). Conn status should show **WS connected**, not Offline/unavailable. Then send again.

## Security

- Empty allowlist = **deny all**
- Do not put MDNs or tokens in git
- Images stored as companion media attachments only — never tool arguments
