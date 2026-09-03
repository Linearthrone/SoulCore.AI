# Security & networking

## Hard rules

- Host binds **loopback only** (`127.0.0.1:7700`).
- No Tailscale **Funnel** / public exposure of Host.
- Companion token required for `/ws` and companion API when set.
- SMS: Kurt E.164 **allowlist**; empty allowlist = deny all.
- Inbound SMS/MMS never becomes tool input.
- Never commit MDNs, tokens, `.env`, or evidence dumps with secrets.

## Reachability matrix

| Client | URL |
| --- | --- |
| Windows on Host PC | `http://127.0.0.1:7700` |
| WSL | **not** loopback — use Tailscale IP or `powershell.exe` |
| Tablet | `https://…ts.net:8443` (prefer HTTPS; Android may block cleartext) |

## Runbooks

- `docs/runbooks/tailscale-serve-soulcore.md`
- `docs/runbooks/sms-gateway-inbound.md` (Security section)
