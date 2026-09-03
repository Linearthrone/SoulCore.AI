# Clients

## ChatDesktop (desk)

- Avalonia app: `House/House.ChatDesktop`
- Connects to Host `/ws` with companion token (`.env` wins over stale User env)
- Shows One Thread (`presence-local`) including SMS channel bubbles

## Victoria Link (phone app)

- `House/House.CompanionAndroid` — Compose client
- Not launched by ALLSTART; build/install separately
- Long-term Link shrink (PROP-1.6 / PROP-3) parked until SMS QA Pass

## SMS gateway (tablet)

- Device: Samsung Tab **SM-X218U** cellular MDN
- **Temporary:** Tasker HTTP inbound + Termux `sms-outbound-poll.sh`
- **Goal:** self-sufficient House gateway app (no Tasker/Play babysitting)

See [SMS workflow](../workflows/sms-gateway.md) and `docs/runbooks/sms-gateway-inbound.md`.
