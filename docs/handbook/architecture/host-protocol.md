# Host & protocol

## Binding

- Host listens on **loopback** `127.0.0.1:7700` (SEC-004).
- Remote clients reach it via **Tailscale serve** (HTTPS `:8443` and/or TCP `:7700`) — never Funnel / public bind.
- Auth: `SOULCORE_COMPANION_API_TOKEN` as `Authorization: Bearer` or `X-Api-Key` on `/ws` and `/api/companion/v1/*`.

## Surfaces

| Surface | Purpose |
| --- | --- |
| `GET /health` | Unauthenticated liveness |
| `WS /ws` | Chat + Presence frames (`chat.done`, tool traces, …) |
| `POST /api/companion/v1/messages/inbound` | Tablet SMS/MMS → One Thread |
| `GET /api/companion/v1/sms/outbound/pending` | Outbound SMS/MMS queue for gateway |
| `POST /api/companion/v1/sms/outbound/{id}/ack` | Gateway ack |
| `POST /api/companion/v1/messages/push` | Proactive companion push |
| `/desktop/view`, `/browser/view` | Presence / Playwright stills |

## Config

- `SoulCore/.env` loaded by `DotEnvLoader` — **overwrites** stale User/Machine env tokens.
- Sections: `Inference`, `Sms`, `Companion`, `Tools`, `UnrealBridge`, …

## Start

```powershell
.\ALLSTART.ps1            # or -RestartHost
.\SoulCore\scripts\start-soulcore.ps1
```

See also: [ALLSTART workflow](../workflows/allstart.md), runbook `docs/runbooks/tailscale-serve-soulcore.md`.
