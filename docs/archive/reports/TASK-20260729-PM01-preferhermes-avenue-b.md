---
type: note
from: PM-01
id: TINA
created: 2026-07-29
re: PreferHermes / ISSUE-002 / TASK-163 OPS
---

# PM decision — PreferHermes Avenue B

**Chose:** Avenue B.

- PreferHermes **tool-loop** = **Ollama** (`CompleteWithToolsAsync` + SoulCore `IToolRegistry`).
- Hermes = **MCP-only** via `CallMcpToolAsync` (no PreferHermes `tools[]` / `CompleteWithToolsAsync` on hermes-agent 0.18.2).
- Keep PreferHermes MCP fail-fast when gateway/key down.
- Do **not** patch Hermes Python; do **not** wait on client-visible `tool_calls` from server agent.

**Rejected for now:** Avenue A (force Hermes client tool_calls) — OPS-163 proves 0.18.2 cannot supply them.

**Ticket:** TASK-164 → BED-01.
