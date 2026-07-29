---
issue_id: ISSUE-20260727-009
discovered: 2026-07-27
severity: P1
status: Fixed
fixed: 2026-07-27
fixed_by: TASK-161
task: TASK-145
---
# [已修复 2026-07-27] BED-161: PreferHermes fail-fast when gateway/key down (no Ollama fallback / hang).

## Problem Description

With `PreferHermes=true` and Hermes gateway stopped mid-test, a `chat.send` that should exercise tools does **not** return `chat.done` / `error` with a clean "hermes gateway unavailable" within 180s. Client sees presence/emotion/loop.want only; Host appears stuck waiting on Hermes HTTP.

Separately, the BED-144 tool path **does** correctly return `hermes gateway unavailable` when Ollama dispatches `desktop_screenshot` while Hermes is down (`PreferHermes=false`, `DesktopBackend=hermes`).

## Reproduction Steps

1. Host: Hermes.Enabled=true, PreferHermes=true.
2. `hermes gateway stop` (confirm `:8642` down).
3. WS chat: "You must call desktop_screenshot now..."
4. Wait 180s → no `chat.done`.

Contrast (Pass): PreferHermes=false, same Hermes-down, prompt forces `desktop_screenshot` → reply quotes `hermes gateway unavailable`; Host log `Hermes MCP invoke aborted: gateway unhealthy for tool=computer_use`.

## Expected Result

PreferHermes primary failure returns promptly (error frame or tool `Success:false` "hermes gateway unavailable") without hanging the WS turn.

## Actual Result

AC6 formal: hung 180s, `chatDone=false`. AC6b diagnostic CallMcpToolAsync path: Pass.

## Evidence

`tmpcode/qa145-evidence/ac6-hermes-down.json`, `ac6b-unavailable.json`, `ac6b-host-log.txt`

## Impact Scope

QA-145 AC6 Fail on PreferHermes path; CallMcpToolAsync unavailable gate OK.
