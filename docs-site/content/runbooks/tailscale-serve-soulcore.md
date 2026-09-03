# Runbook: Tailscale serve → SoulCore Host (:7700)

**Audience:** OPS / phone-companion setup  
**Goal:** Let a phone on the same Tailscale tailnet reach `SoulCore.Host` without opening a LAN / non-loopback bind (SEC-004 / SEC-152).  
**Host stays:** `127.0.0.1:7700` — Tailscale **serve** proxies onto loopback.  
**Do not:** bind Host to `0.0.0.0`, LAN IP, or document Funnel (public internet) as the Phase 0 path.

Related: SEC-152 (`docs/agents/log/TASK-20260727-152-SEC01-to-PM01.md`), PROP-COMPANION-01.

---

## 0. Prerequisites

| Piece | Requirement |
| --- | --- |
| Host PC | Tailscale installed + logged in; SoulCore Host running on loopback `:7700` |
| Phone | Tailscale app installed + same Tailscale account / tailnet |
| Admin console | Tailnet **HTTPS** certificates enabled (DNS → Enable HTTPS) if using HTTPS serve |
| Host config | `AllowedHosts` must accept the MagicDNS name / Tailscale IP (see §4) |

Helper script (optional): `SoulCore/scripts/tailscale-serve-soulcore.ps1`

---

## 1. Install Tailscale

### Windows (Host PC)

1. Install from https://tailscale.com/download/windows  
2. Sign in; confirm the service is running:

```powershell
Get-Service Tailscale
tailscale version
tailscale status
tailscale ip -4
```

Expect a `100.x.y.z` address and a MagicDNS name like `<machine>.<tailnet>.ts.net`.

### Android (phone)

1. Install **Tailscale** from Play Store / F-Droid.  
2. Sign in with the **same** account as the Host PC.  
3. Toggle the VPN on; confirm the phone appears in `tailscale status` on the PC.

---

## 2. Start SoulCore Host (loopback only)

```powershell
# From repo root
.\SoulCore\scripts\start-soulcore.ps1
# Or:
dotnet run --project SoulCore/SoulCore.Host -c Release
```

Verify locally (must work before serve matters):

```powershell
curl.exe -sS http://127.0.0.1:7700/health
```

Expect JSON with `"status":"ok"` and `"bind":"127.0.0.1"`.

---

## 3. Expose :7700 with Tailscale serve (tailnet only)

CLI notes for Tailscale **1.98+** on Windows:

- Put flags **before** the port target: `tailscale serve --https=8443 --bg --yes 7700`
- Target is a bare port (`7700`), not `http://127.0.0.1:7700` (that form returns `invalid argument format`)
- Default HTTPS on **443** fails if something else already serves 443 (e.g. existing Ollama proxy) → use **8443**
- Prefer **serve** (tailnet). Do **not** use `tailscale funnel` for SoulCore Phase 0

### Recommended A — HTTPS reverse proxy (best for phone `wss://`)

```powershell
tailscale serve --https=8443 --bg --yes 7700
tailscale serve status
```

Expected status snippet:

```text
https://<machine>.<tailnet>.ts.net:8443 (tailnet only)
|-- / proxy http://127.0.0.1:7700
```

Disable later:

```powershell
tailscale serve --https=8443 off
```

### Recommended B — TCP forward (plain `ws://` via Tailscale IP)

```powershell
tailscale serve --tcp=7700 --bg --yes 7700
tailscale serve status
```

Expected:

```text
|-- tcp://100.x.y.z:7700
|--> tcp://127.0.0.1:7700
```

Disable later:

```powershell
tailscale serve --tcp=7700 off
```

Both A and B can be active together. A is preferred for Android (`wss://`).

### Optional helper

```powershell
.\SoulCore\scripts\tailscale-serve-soulcore.ps1          # apply A+B
.\SoulCore\scripts\tailscale-serve-soulcore.ps1 -Status  # show serve status
.\SoulCore\scripts\tailscale-serve-soulcore.ps1 -Off     # remove A+B only
```

---

## 4. AllowedHosts (required or health returns HTTP 400)

`SoulCore.Host` ships with:

```json
"AllowedHosts": "localhost;127.0.0.1"
```

Tailscale proxies send `Host: <MagicDNS>` or `Host: <100.x…>`. Without expanding `AllowedHosts`, Kestrel returns **HTTP 400 Invalid Hostname** even when serve is configured correctly.

**OPS/SEC-approved pattern (still loopback bind):** expand hosts — do **not** change `Host:BindAddress`.

Example (replace with your MagicDNS + TS IP from `tailscale status` / `tailscale ip -4`):

```json
"AllowedHosts": "localhost;127.0.0.1;kaia-reimagined.tailbf9ec2.ts.net;100.71.223.95"
```

Or process env before start:

```powershell
$env:ASPNETCORE_ALLOWEDHOSTS = "localhost;127.0.0.1;kaia-reimagined.tailbf9ec2.ts.net;100.71.223.95"
```

Restart Host after changing. Prefer explicit names over `*`; any widen needs SEC awareness (SEC-152).

---

## 5. Android / companion connect URLs

Resolve names on the Host PC:

```powershell
tailscale ip -4
# MagicDNS: from `tailscale status` / DNSName (strip trailing dot)
```

| Setting | Example (this machine, 2026-07-27) | Notes |
| --- | --- | --- |
| Health (HTTPS serve) | `https://kaia-reimagined.tailbf9ec2.ts.net:8443/health` | After AllowedHosts fix |
| WS (HTTPS serve) | `wss://kaia-reimagined.tailbf9ec2.ts.net:8443/ws` | Preferred for phone |
| Health (TCP serve) | `http://100.71.223.95:7700/health` | Tailscale IP |
| WS (TCP serve) | `ws://100.71.223.95:7700/ws` | Plain WS on tailnet |

Android companion settings should store the **WS base** (or full `…/ws` URL) plus whatever auth token FED/SEC specifies — never LAN `192.168…` for Phase 0.

---

## 6. Verify

### From Host PC (after AllowedHosts fix)

```powershell
# Local (always)
curl.exe -sS http://127.0.0.1:7700/health

# Via HTTPS serve
curl.exe -sS -k https://kaia-reimagined.tailbf9ec2.ts.net:8443/health

# Via TCP serve (Tailscale IP)
curl.exe -sS http://100.71.223.95:7700/health
```

Expect `"status":"ok"`.

### From phone

1. Tailscale VPN **on**.  
2. Browser: open the HTTPS health URL above → JSON ok.  
3. Companion app: connect WS URL → streaming chat frames.

If health is **400 Invalid Hostname** → fix §4.  
If connection times out → phone offline in `tailscale status`, or serve not running (`tailscale serve status`).  
If local health fails → start Host first (§2).

---

## 7. Security reminders (SEC-152)

- Host **bind stays** `127.0.0.1` — serve is the remote path.  
- Prefer **serve** (tailnet ACL) over **funnel** (public).  
- Once phone uses serve, Host WS should require the companion token (Keystore on Android).  
- `/health` may stay open on loopback for ops; do not put secrets in health JSON.  
- Existing unrelated proxies (e.g. Ollama on `:443`) can coexist; do not `tailscale serve reset` blindly.

---

## 8. If Tailscale is not installed

```powershell
Get-Command tailscale -ErrorAction SilentlyContinue
# empty → install from https://tailscale.com/download/windows
# then re-run §1–§3
```

Document in the OPS report: version missing, commands not runnable, install blocked reason.
)
