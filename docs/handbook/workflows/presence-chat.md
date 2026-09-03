# Presence chat workflow

1. Host running; ChatDesktop started with token from `SoulCore/.env`.
2. Client opens `WS /ws` with `X-Api-Key` / Bearer.
3. User messages and assistant replies are `chat.done` frames on session `presence-local` (One Thread).
4. SMS channel uses the same session — desk and phone share memory.
5. Conn status: **WS connected** is required for chat; `/health` alone is not enough.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Host up, chat dead | Token mismatch; User env poisoning `.env` |
| SMS on Host logs, no bubble | ChatDesktop WS not connected / wrong session |
| 401 everywhere | Restart Host after clearing stale User `SOULCORE_COMPANION_API_TOKEN` |
