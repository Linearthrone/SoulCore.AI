---
type: issue
id: ISSUE-20260729-001
from: QA-01 / PM-01
priority: P1
status: Fixed
resolved: 2026-07-29
reopened: 2026-07-29
gate: QA-142 retest-2 / retest-3
related: TASK-142, TASK-162, TASK-165, TASK-166, BED-162, BED-165, BED-166
related_fixed: ISSUE-20260727-004, ISSUE-20260727-005
fix: docs/agents/reports/TASK-20260729-166-BED01-to-PM01.md
prior_fix: docs/agents/reports/TASK-20260729-165-BED01-to-PM01.md
---

# [已修复 2026-07-29] ISSUE-20260729-001: Exact NL workflow prompts skip tools / wrong-tool escape / /v1 args 400

# ISSUE-20260729-001: Exact NL workflow prompts skip tools (prose-only) + forced wrong-tool escape + /v1 arguments 400

## Summary

On Ollama `qwen2.5:14b`, exact Phase E AC prompts for workflows sometimes
return prose without dispatching `workflow_create` / `workflow_execute`.
Forced recovery prompts that name the tool already Pass. Session history
(ISSUE-004) and nested step args (ISSUE-005) are Fixed — residual Fail is
NL tool selection / guidance.

**Reopen (QA-142 Retest-2):** BED-162 set `ForceToolName` + `/v1` `tool_choice`,
but still advertised the **full** `tools[]`. Model escaped to sibling tools
(e.g. `task_list`) and Host executed them — AC6 Fail despite `forceTool=` logs.

**Reopen (post BED-165):** Exclusive ForceTool works, but when session history
includes prior assistant `tool_calls` whose `function.arguments` are JSON
**objects**, Ollama `/v1/chat/completions` returns 400:

`cannot unmarshal object into Go struct field .messages.tool_calls.function.arguments of type string`

→ no dispatch → QA-142 AC5–7 still blocked.

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
4. **Retest-2:** Host log shows `forceTool=workflow_execute` but dispatch is a
   non-forced sibling tool — AC6 still Fail.
5. **Post BED-165:** ForceTool exclusivity OK on cold turn; with prior
   tool_calls in session history, `/v1` 400 on object-form `arguments`.

## Expected

Same prompts dispatch `workflow_create` then `workflow_execute` (with
`all=true` on full-run phrasing), using session history for ids. When
`ForceToolName` is set, only that tool is advertised and executable on
iteration 0. Outbound `/v1` bodies stringify `arguments` even when history
stored them as objects.

## Fix (BED-162)

- Richer workflow tool descriptions (when-to-use + AC trigger phrases).
- System `[Tools]` agency guidance on the tool-loop path.
- `WorkflowToolIntent` + Ollama `ToolLoopOptions.ForceToolName` on iteration 0
  via OpenAI-compat `/v1/chat/completions` (native `/api/chat` ignores
  `tool_choice` on Ollama 0.32.4).
- Soft create-time tool inference for recall/speak step descriptions.

## Fix (BED-165 — reopen)

- Exclusive `tools[]` (forced name only) on ForceToolName iteration 0.
- Hard `tool_choice` retained on `/v1/chat/completions`.
- Hard refuse: never `ExecuteAsync` a non-forced name while force is active.
- BED-162 `WorkflowToolIntent` + BED-164 PreferHermes Avenue B kept intact
  (ForceToolName also passed on PreferHermes→Ollama path).

See `docs/agents/reports/TASK-20260729-165-BED01-to-PM01.md`.

## Fix (BED-166 — /v1 arguments string)

- When posting ForceTool turns to `/v1/chat/completions`, clone messages via
  `ToOpenAiWireMessages` so every `tool_calls[].function.arguments` is a JSON
  **string** on the wire (OpenAI / Ollama Go contract).
- In-memory / `/api/chat` conversation keeps object-form args unchanged.
- Unit: `ForceToolName_HistoryObjectArguments_AreStringifiedOnV1Wire`.

See `docs/agents/reports/TASK-20260729-166-BED01-to-PM01.md`.
