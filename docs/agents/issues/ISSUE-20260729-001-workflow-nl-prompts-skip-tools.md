---
type: issue
id: ISSUE-20260729-001
from: QA-01 / PM-01
priority: P1
status: Fixed
resolved: 2026-07-29
gate: QA-142 retest
related: TASK-142, TASK-162, BED-162
related_fixed: ISSUE-20260727-004, ISSUE-20260727-005
fix: docs/agents/reports/TASK-20260729-162-BED01-to-PM01.md
---

# [已修复 2026-07-29] ISSUE-20260729-001: Exact NL workflow prompts skip tools (prose-only)

# ISSUE-20260729-001: Exact NL workflow prompts skip tools (prose-only)

## Summary

On Ollama `qwen2.5:14b`, exact Phase E AC prompts for workflows sometimes
return prose without dispatching `workflow_create` / `workflow_execute`.
Forced recovery prompts that name the tool already Pass. Session history
(ISSUE-004) and nested step args (ISSUE-005) are Fixed — residual Fail is
NL tool selection / guidance.

## Severity

**P1** — blocks Phase E formal exit on AC5–7 exact prompts (create / run /
re-run workflow) without requiring the user to name tools.

## Repro (QA-142 class)

1. Host + Ollama `qwen2.5:14b`, `UseToolLoop=true`, Hermes disabled.
2. Exact prompts (no tool names in user text):
   - `create a workflow to: 1) recall a memory, 2) speak the memory`
   - `run that workflow`
   - `run that workflow again`
3. Observe: model replies in prose / asks for clarification; Host log has no
   `Ollama tool dispatch: … name=workflow_*`.

## Expected

Same prompts dispatch `workflow_create` then `workflow_execute` (with
`all=true` on full-run phrasing), using session history for ids.

## Fix (BED-162)

- Richer workflow tool descriptions (when-to-use + AC trigger phrases).
- System `[Tools]` agency guidance on the tool-loop path.
- `WorkflowToolIntent` + Ollama `ToolLoopOptions.ForceToolName` on iteration 0
  via OpenAI-compat `/v1/chat/completions` (native `/api/chat` ignores
  `tool_choice` on Ollama 0.32.4).
- Soft create-time tool inference for recall/speak step descriptions.
- PreferHermes unchanged (ForceToolName not passed).

See `docs/agents/reports/TASK-20260729-162-BED01-to-PM01.md`.
