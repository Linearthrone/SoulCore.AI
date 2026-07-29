---
type: issue
id: ISSUE-20260727-005
from: QA-01
priority: P0
status: Fixed
resolved: 2026-07-27
gate: QA-142
related: TASK-141, TASK-142, TASK-159, BED-159
fix: docs/agents/reports/TASK-20260727-159-BED01-to-PM01.md
---

# [已修复 2026-07-27] ISSUE-20260727-005: workflow_execute dispatches nested tools with empty `{}`

# ISSUE-20260727-005: workflow step empty tool args

## Summary

`WorkflowExecuteTool` always called `IToolRegistry.ExecuteAsync(toolName, {})`
for steps that named a tool. Nested tools that require arguments
(`recall_memory` → `query`, `speak` → `text`) failed during QA-142
workflow execute (AC6), blocking Phase E exit.

## Severity

**P0** for Phase E gate — Victoria can create a workflow but cannot usefully
execute tool-bearing steps.

## Repro (QA-142)

1. Create workflow: step1 `tool=recall_memory`, step2 `tool=speak`
   (descriptions only; no per-step args — schema had none).
2. `workflow_execute` with `all=true`.
3. Nested dispatch uses `EmptyToolArgs` (`{}`).
4. `recall_memory` returns `error: recall_memory requires 'query' (string).`
   (and/or speak fails similarly on `text`).

## Root cause

BED-141 intentionally documented empty args:

> With `tool`: dispatch via `IToolRegistry.ExecuteAsync(name, {})`
> (empty args object — step schema has no tool-args field).

## Expected fix (BED-159)

- Optional `steps[].args` object on create + persistence.
- At execute time: use `args`, and/or map `description` into the target
  tool's primary required string parameter.

## Resolution

Fixed in TASK-159 (`WorkflowStepToolArgs` + `WorkflowStep.Args`). See
`docs/agents/reports/TASK-20260727-159-BED01-to-PM01.md`.
