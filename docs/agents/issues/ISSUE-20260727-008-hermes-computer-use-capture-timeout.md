---
issue_id: ISSUE-20260727-008
discovered: 2026-07-27
severity: P1
status: Pending Fix
task: TASK-145
---

## Problem Description

Hermes MCP / native `computer_use` screenshot path fails with `TimeoutError` in `cua_backend.py` (`capture failed:`). Browser capture via Hermes `browser_snapshot` fails with npm `agent-browser` install/exec timeout (30s).

## Reproduction Steps

1. Hermes up with `computer_use` MCP + PreferHermes chat (or Hermes-native tools).
2. Prompt: take a desktop screenshot / capture browser tab.
3. Hermes log shows capture timeout; chat reports failure.

## Expected Result

Screenshot/capture returns a usable path or image payload; model reports success.

## Actual Result

```text
ERROR tools.computer_use.tool: computer_use capture failed
...
TimeoutError
WARNING agent.tool_executor: Tool computer_use returned error (44.09s): {"error": "capture failed: "}
```

Browser: `Command timed out after 30 seconds` / `npm warn exec ... agent-browser@0.33.0`

## Evidence

`tmpcode/qa145-evidence/ac2-screenshot.json`, `ac2-hermes-mcp.txt`, `ac3-browser.json`

## Impact Scope

QA-145 AC2/AC3 Fail (capture success). Routing to Hermes tools partially observed for desktop.

## Suggested Fix (PM → OPS)

- Fix/update `cua-driver` (log suggests v0.12.6 available vs v0.7.1).
- Pre-install `agent-browser` / raise browser MCP timeout.
- Verify house_victoria `browser_bridge_capture_tab` is the tool PreferHermes should call (ISSUE-007).
