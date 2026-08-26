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

## Tablet SMS → Host (Termux + Tasker)

Victoria’s SM-X218U posts inbound SMS to Host over **Tailscale serve** (no Funnel). Script: repo-root `sms-to-victoria.sh`. **Never** put `SOULCORE_COMPANION_API_TOKEN` in the script or in git.

**This PC (home-pc) reachability — do not curl `127.0.0.1:7700` from WSL:**

| Path | URL |
| --- | --- |
| Windows loopback | `http://127.0.0.1:7700` (powershell.exe / curl.exe on Windows only) |
| Tailscale TCP (tablet default) | `http://100.71.223.95:7700` |
| Tailscale HTTPS | `https://kaia-reimagined.tailbf9ec2.ts.net:8443` |

Host `.env` on this machine: companion token **length 63**, Kurt allowlist set (do not paste either value). Tablet Tailscale VPN must be on.

### 1) Termux: copy script + token file

On the Tab (F-Droid Termux + `pkg install curl jq`). Do **not** paste `nano` in a bulk command block — it swallows the rest of the paste.

**Script** — pull from GitHub `main` (no token in this file):

```bash
mkdir -p ~/bin ~/.config/soulcore ~/.termux/tasker
curl -fsSL -o ~/bin/sms-to-victoria.sh \
  https://raw.githubusercontent.com/Linearthrone/SoulCore.AI/main/sms-to-victoria.sh
chmod +x ~/bin/sms-to-victoria.sh
ln -sf ~/bin/sms-to-victoria.sh ~/.termux/tasker/sms-to-victoria.sh
head -n 5 ~/bin/sms-to-victoria.sh
# first line must be #!/usr/bin/env bash — if you still have TOKEN='...' this is the old draft
```

If Termux already has a clone (prompt `~/repos`):

```bash
cp ~/repos/SoulCore.AI/sms-to-victoria.sh ~/bin/sms-to-victoria.sh
chmod +x ~/bin/sms-to-victoria.sh
```

**Token file** — one line, same value as Host `SoulCore/.env`. Paste on the tablet only; do not echo it into chat or git.

```bash
# paste the token, then Ctrl-D
cat > ~/.config/soulcore/companion.token
chmod 600 ~/.config/soulcore/companion.token
wc -c ~/.config/soulcore/companion.token
# Host .env token is 63 chars; 64 usually means a trailing newline (ok)
```

Optional HTTPS instead of TCP:

```bash
echo 'export SOULCORE_HOST=https://kaia-reimagined.tailbf9ec2.ts.net:8443' >> ~/.bashrc
```

Enable Termux:Tasker (once):

```bash
# ~/.termux/termux.properties — add:
# allow-external-apps=true
termux-reload-settings
```

### 2) Smoke test (Termux)

Use Kurt’s **allowlisted** E.164 (the `SOULCORE_Sms__KurtAllowlistE164` value on Host — not committed):

```bash
~/bin/sms-to-victoria.sh --health
# expect a line GET http://100.71.223.95:7700/health then JSON status=ok
# If you get {"dropped":true} immediately, ~/bin still has the old draft
# (it treats --health / --from as the SMS sender). Re-curl the script above.

~/bin/sms-to-victoria.sh --from '+1XXXXXXXXXX' --text 'gateway smoke from tablet'
# expect: host=... tokenPresent=true tokenLen=63 from=+1... textLen=...
#         HTTP 200
#         {"ok":true,"dropped":false,"replyText":"..."}
# dropped:true with a real +1 number = Host allowlist mismatch (not this script)
```

Do **not** `curl 127.0.0.1:7700` from WSL. From WSL use `powershell.exe` or `http://100.71.223.95:7700`.

### 3) Tasker profile (exact)

Install **Termux:Tasker** (same F-Droid source as Termux). Profile:

1. **Profiles → + → Event → Phone → Received Text**
   - Type: **Any** (or SMS)
   - Sender: leave empty (Host allowlist drops unknowns)
2. New task name: `SMS to Victoria`
3. **+ → Plugin → Termux:Tasker → Configuration**
   - **Executable:** `sms-to-victoria.sh`  
     (this is `~/.termux/tasker/sms-to-victoria.sh` → `~/bin/sms-to-victoria.sh`)
   - **Arguments:** `%SMSRF` `%SMSRB`  
     (`%SMSRF` = from, `%SMSRB` = body; extra words join as body)
   - **Working directory:** `$HOME`
   - **Timeout (seconds):** `60`
   - **Terminal session:** off
4. Back out and **tick** to save. Long-press the profile → confirm it is **On**.
5. Grant Tasker **SMS / Notification** access if Android asks.

Manual Tasker test: **Tasks → SMS to Victoria → Play** after setting `%SMSRF` / `%SMSRB` in Variables, or send a real SMS from Kurt’s allowlisted phone to the Tab MDN.

PROP-1.3 will SMS `replyText` back to Kurt automatically; this bridge is inbound-only.

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

### ChatDesktop: Host “up” but chat says host down / WS auth

`/health` is unauthenticated → Services can show Host **up**. Chat uses `/ws`, which requires the companion token (`X-Api-Key` from ChatDesktop; Host also accepts Bearer). If ChatDesktop has a missing/stale token, Conn status shows **WS auth** (not a vague down) and send fails.

Restart ChatDesktop after `.env` changes (`start-desktopgui.ps1` / `CompanionToken` — `.env` wins over stale User env). Expect **WS connected**.

Quick probe (never prints the secret):

```powershell
.\SoulCore\scripts\ws-companion-auth-probe.ps1
# or: .\SoulCore\scripts\ws-companion-auth-probe.ps1 -Port 7701
```

Expect `X_API_KEY => CONNECTED` when `SOULCORE_COMPANION_API_TOKEN` is set on Host.

## Security

- Empty allowlist = **deny all**
- Do not put MDNs or tokens in git
- Images stored as companion media attachments only — never tool arguments
