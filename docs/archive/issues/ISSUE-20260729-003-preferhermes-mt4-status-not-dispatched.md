---
type: issue
id: ISSUE-20260729-003
from: QA-01 / PM-01
priority: P1
status: Fixed
resolved: 2026-07-29
qa_verified: 2026-07-29
gate: QA-145 Avenue B AC4
related: TASK-145, TASK-164, TASK-165, TASK-166, TASK-167, BED-164, BED-165, BED-166, BED-167
fix: docs/agents/reports/TASK-20260729-167-BED01-to-PM01.md
qa_live: tmpcode/qa145-evidence/ac4-bed167-20260729-143933/
qa_context: docs/agents/reports/TASK-20260726-145-QA01-to-PM01.md
---

# [已修复 2026-07-29] ISSUE-20260729-003: PreferHermes Avenue B — mt4_status never dispatched (task_* escape)

## QA live verify (2026-07-29, post BED-167)

**Fixed (live).** PreferHermes Avenue B, prompt `"what's my MT4 status?"`:

```text
MT4 NL intent matched: intent=Status forceTool=mt4_status
Ollama tool dispatch: … name=mt4_status
Hermes MCP invoke start: tool=mt4_status
```

No `task_create` / `task_get` escape. Evidence:
`tmpcode/qa145-evidence/ac4-bed167-20260729-143933/`. AC1 same Host
`provider=ollama`. See QA-145 report AC4 retest section.

# ISSUE-20260729-003: PreferHermes Avenue B AC4 — model never calls `mt4_status`

## Summary

After BED-164 Avenue B (PreferHermes tool-loop on **Ollama**; Hermes
**MCP-only**), desktop / browser / trade MCP paths Pass, but QA-145 **AC4**
still Fails: prompts like `"what's my MT4 status?"` never dispatch SoulCore
`mt4_status`. The model instead calls `task_create` / `task_get` (Victoria
task tools) because "status" collides with task semantics and `mt4_status`
was not ForceTool-forced the way workflows are (BED-162/165).

## Severity

**P1** — blocks QA-145 Phase F AC4 (MT4 read via MCP) on the Avenue B path.

## Repro

1. Host with PreferHermes Avenue B (`PreferHermes=true`, Hermes MCP up,
   Ollama tool-loop, `AllowMt4Read=true`, `Mt4Backend=hermes`).
2. WS `chat.send`: `"what's my MT4 status?"` (or force-`mt4_status` phrasing).
3. Observe: Host may dispatch `task_create` / `task_get`; no
   `Ollama tool dispatch: … name=mt4_status` → no `CallMcpToolAsync(mt4_status)`.

## Expected

Same prompt sets `ForceToolName=mt4_status` (BED-165 exclusive `tools[]` +
hard `tool_choice` + refuse wrong names), Host dispatches SoulCore
`mt4_status` → Hermes `CallMcpToolAsync("mt4_status")`. Task-status prompts
must not force MT4.

## Fix (BED-167)

- `Mt4ToolIntent` (mirror `WorkflowToolIntent`) → `ForceToolName=mt4_status`.
- Hardened `mt4_status` description + `[Tools]` agency guidance.
- PreferHermes Avenue B, BED-165 exclusivity, BED-166 `/v1` string args preserved.

See `docs/agents/reports/TASK-20260729-167-BED01-to-PM01.md`.
