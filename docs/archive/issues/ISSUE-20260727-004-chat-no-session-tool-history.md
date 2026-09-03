---
type: issue
id: ISSUE-20260727-004
from: QA-01
priority: P1
status: Fixed
resolved: 2026-07-27
gate: QA-142
related: TASK-142, TASK-158, BED-158
fix: ChatWebSocketHandler now retains per-sessionId user/assistant/tool messages and replays them into CompleteWithToolsAsync (BED-158).
---

# [已修复 2026-07-27] ISSUE-20260727-004: Chat has no per-sessionId tool/chat history

# ISSUE-20260727-004: Chat has no per-sessionId tool/chat history

## Summary

Each `chat.send` builds a fresh `messages[]` of only `[system preamble, user text]`. Prior turns — including `task_create` / `workflow_create` tool results that carry integer IDs — are discarded. Multi-turn pronouns in QA-142 ("what's the status of that task?", "mark that task done", "run that workflow") therefore cannot resolve to the ID returned on the previous turn.

## Severity

**P1** — blocks Phase E exit gate QA-142 (task get/update + workflow execute follow-ups).

## Repro (observed during QA-142)

1. Host + Ollama with UseToolLoop=true; task/workflow tools registered.
2. Same `sessionId` across turns.
3. Turn 1: "create a task to remember to review the charter" → `task_create` returns `id=N`.
4. Turn 2: "what's the status of that task?" → model has no prior tool result / assistant text; cannot call `task_get` with `id=N`.

## Root cause

`ChatWebSocketHandler.CompleteChatWithToolsAsync` never loaded or persisted conversation state keyed by `chat.send` payload `sessionId`. `TrackingToolRegistry` recorded tool names for Strategy A only, not result Content for later turns.

## Fix (BED-158)

- `IChatSessionHistoryStore` / `ChatSessionHistoryStore` — in-memory, bounded per sessionId.
- Tool-loop path prepends prior messages; after success appends user + synthetic tool_calls + role:tool results + assistant reply.
- Key: payload `sessionId` when present, else `ws:{connectionGuid}`.
- Config: `ChatWs:MaxSessionHistoryMessages` (default 40).
