# Memory, charter, SoulLoop

## Memory

- SQLite store under LocalAppData / configured `Memory:DbPath`.
- Episodic writes from chat, SMS, tools; optional embeddings.
- Tools: `recall_memory`, `store_memory`.

## Charter

- Locked product charter / safety anchors live in Core + seeded memory.
- Do not casually rewrite charter locks; see `docs/agents/PRODUCT_ROOT.md` for current product scoreboard (trim/update when stale).

## SoulLoop

- Hosted background loop for proactive ticks / continuity (when enabled).
- Companion outbound messenger can push unsolicited `chat.done` to Presence clients.
- Must not spam the SMS carrier — SMS outbound is rate-limited separately (`Sms:*` options).
