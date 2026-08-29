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

## Tablet SMS → Host (PROP-1 kill #1)

Victoria’s SM-X218U posts inbound SMS to Host over **Tailscale serve** (no Funnel).
**Never** put `SOULCORE_COMPANION_API_TOKEN` in scripts, Tasker exports, or git.

**Preferred bridge: Tasker HTTP Request (no Termux plugin).**  
Termux `sms-to-victoria.sh` remains a smoke/fallback path. GitHub Termux:Tasker often fails to select in Tasker — do **not** block on that plugin.

**This PC (home-pc) reachability — do not curl `127.0.0.1:7700` from WSL:**

| Path | URL |
| --- | --- |
| Windows loopback | `http://127.0.0.1:7700` (powershell.exe / curl.exe on Windows only) |
| Tailscale TCP (tablet default) | `http://100.71.223.95:7700` |
| Tailscale HTTPS | `https://kaia-reimagined.tailbf9ec2.ts.net:8443` |

Host `.env` on this machine: companion token **length 63**, Kurt allowlist set (do not paste either value). Tablet Tailscale VPN must be on.

### A) Primary — Tasker HTTP (do this first)

No Termux:Tasker. No Intent. Tasker posts JSON straight to Host.

#### Once on the tablet

1. Install **Tasker** (Play / official). Grant **SMS** + **Notifications** when asked.
2. Confirm Tailscale is **Connected** (can reach `100.71.223.95`).
3. **Vars → +** → name `SOULCORE_TOKEN` → paste the same companion token as Host `.env` (length 63). Do **not** export this project to git/chat.
4. Optional Var `SOULCORE_HOST` = `http://100.71.223.95:7700` (no trailing slash).

#### Profile

1. **Profiles → + → Event → Phone → Received Text**
   - Type: **Any** (or SMS)
   - Sender: leave empty (Host allowlist drops unknowns)
2. New task: `SMS to Victoria HTTP`
3. Action 1 — **Code → JavaScriptlet** (escapes quotes/newlines in the SMS body):

```javascript
var from = local('SMSRF') || global('SMSRF') || '';
var text = local('SMSRB') || global('SMSRB') || '';
setLocal('sc_body', JSON.stringify({ fromE164: String(from), text: String(text) }));
setLocal('sc_from', String(from));
setLocal('sc_text_len', String(text.length));
```

4. Action 2 — **Net → HTTP Request**
   - Method: **POST**
   - URL: `%SOULCORE_HOST/api/companion/v1/messages/inbound`  
     (or hardcode `http://100.71.223.95:7700/api/companion/v1/messages/inbound`)
   - Headers (two lines):

```text
Content-Type: application/json; charset=utf-8
X-Api-Key: %SOULCORE_TOKEN
```

   - Body / File: `%sc_body`
   - Timeout: **180** seconds (ChatDesktop can show the SMS over `/ws` before HTTP returns)
   - Continue Task After Error: **on** (so you can flash `%http_data` / `%err` while debugging)

5. Action 3 (optional while debugging) — **Alert → Flash**: `HTTP %http_code from=%sc_from len=%sc_text_len`
6. Save. Long-press profile → **On**.

#### Manual Play (before a real SMS)

1. **Vars**: set `%SMSRF` = Kurt’s allowlisted E.164 (e.g. `+1…` or 10-digit — Host normalizes).
2. Set `%SMSRB` = `tasker http smoke`
3. **Tasks → SMS to Victoria HTTP → Play**
4. Expect Flash `HTTP 200` (or check Host log / ChatDesktop user bubble + Victoria reply).
5. Then send a **real SMS** from Kurt’s phone to the Tab MDN → same result = **#1 Done**.

**JSON footgun:** do **not** hand-type `{"fromE164":"%SMSRF","text":"%SMSRB"}` if the SMS can contain `"` or newlines — use the JavaScriptlet. Host returns `400 invalid JSON body` when the body is mangled.

**401:** `%SOULCORE_TOKEN` ≠ Host token (stale User env on PC vs `.env` — see Auth section below).

**200 dropped:true:** sender not on `SOULCORE_Sms__KurtAllowlistE164` (normalize both sides to E.164).

PROP-1.3 will SMS `replyText` back to Kurt automatically; this bridge is inbound-only.

---

### B) Fallback — Termux script + smoke (optional)

Use when you want a Termux curl path, or Tasker HTTP is blocked by OEM network rules.

#### 1) Termux: copy script + token file

On the Tab (F-Droid Termux + `pkg install curl jq`). Do **not** paste `nano` in a bulk command block — it swallows the rest of the paste.

**Script** — pull from GitHub `main` (no token in this file):

```bash
mkdir -p ~/bin ~/.config/soulcore ~/.termux/tasker
curl -fsSL -o ~/bin/sms-to-victoria.sh \
  https://raw.githubusercontent.com/Linearthrone/SoulCore.AI/main/sms-to-victoria.sh
curl -fsSL -o ~/bin/sms-ping.sh \
  https://raw.githubusercontent.com/Linearthrone/SoulCore.AI/main/sms-ping.sh
chmod +x ~/bin/sms-to-victoria.sh ~/bin/sms-ping.sh
ln -sf ~/bin/sms-to-victoria.sh ~/.termux/tasker/sms-to-victoria.sh
ln -sf ~/bin/sms-ping.sh ~/.termux/tasker/sms-ping.sh
head -n 5 ~/bin/sms-to-victoria.sh
# first line must be #!/usr/bin/env bash — if you still have TOKEN='...' this is the old draft
```

If Termux already has a clone (prompt `~/repos`):

```bash
cp ~/repos/SoulCore.AI/sms-to-victoria.sh ~/repos/SoulCore.AI/sms-ping.sh ~/bin/
chmod +x ~/bin/sms-to-victoria.sh ~/bin/sms-ping.sh
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

Enable external apps for Intent fallback (once):

```bash
# ~/.termux/termux.properties — add:
# allow-external-apps=true
termux-reload-settings
```

#### 2) Smoke test (Termux)

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
# ChatDesktop can show the user bubble before HTTP returns (WS is sent first).
# curl exit 28 = waited for the model reply past SOULCORE_CURL_MAX_TIME (default 180s).
# dropped:true with a real +1 number = Host allowlist mismatch (not this script)
```

Do **not** `curl 127.0.0.1:7700` from WSL. From WSL use `powershell.exe` or `http://100.71.223.95:7700`.

#### 3) Termux:Tasker plugin (only if F-Droid plugin works)

Same F-Droid source as Termux. Profile task:

- **Executable:** `sms-to-victoria.sh`
- **Arguments:** `%SMSRF` `%SMSRB`
- **Working directory:** `$HOME`
- **Timeout:** `180`
- **Terminal session:** off

If the plugin **won’t select** in Tasker, skip this — use **§A HTTP** or **§C Intent**.

---

### C) Fallback — Tasker Send Intent → Termux `RUN_COMMAND`

Use when HTTP is fine from Termux curl but you still want Tasker → script (no Termux:Tasker plugin).

1. Termux: `allow-external-apps=true` + `termux-reload-settings` (see §B).
2. Confirm ping first (proves Intent reaches Termux **before** Host):

**Task** `SMS ping Termux` → **System → Send Intent**

| Field | Value |
| --- | --- |
| Action | `com.termux.RUN_COMMAND` |
| Cat | **None** (or Default) |
| Mime Type | (empty) |
| Data | (empty) |
| Extra | `com.termux.RUN_COMMAND_PATH:/data/data/com.termux/files/home/bin/sms-ping.sh` |
| Extra | `com.termux.RUN_COMMAND_ARGUMENTS:%SMSRF %SMSRB` |
| Extra | `com.termux.RUN_COMMAND_WORKDIR:/data/data/com.termux/files/home` |
| Extra | `com.termux.RUN_COMMAND_BACKGROUND:true` |
| Package | `com.termux` |
| Class | `com.termux.app.RunCommandService` |
| Target | **Service** |

Play the task → in Termux: `tail -n 20 ~/sms-forward.log`  
Expect a `ping ok` line. **No log = Intent never reached Termux** (wrong class/target, `allow-external-apps` off, or OEM kill).

3. When ping works, duplicate the Intent and change `PATH` to  
   `/data/data/com.termux/files/home/bin/sms-to-victoria.sh`  
   (same `ARGUMENTS` / `WORKDIR` / `BACKGROUND`). Timeout on Host is still up to 180s; ChatDesktop may show the SMS first.

4. Wire **Received Text** → that task. Real SMS → ChatDesktop = **#1 Done**.

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
