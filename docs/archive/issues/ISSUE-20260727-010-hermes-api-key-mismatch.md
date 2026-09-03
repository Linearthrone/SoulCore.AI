---
issue_id: ISSUE-20260727-010
discovered: 2026-07-27
severity: P2
status: Fixed
fixed: 2026-07-27
fixed_by: TASK-161
task: TASK-145
---
# [已修复 2026-07-27] BED-161: fail-fast + API key preflight/docs; QA must align SOULCORE_HERMES_API_KEY with Hermes API_SERVER_KEY before retest.

## Problem Description

`SoulCore/.env` `SOULCORE_HERMES_API_KEY` does **not** match Hermes `%LOCALAPPDATA%\hermes\.env` `API_SERVER_KEY`. Bearer auth with the SoulCore key returns **401** on `GET /v1/models`; Hermes key returns 200.

QA-145 had to override the process env with the Hermes `API_SERVER_KEY` (not written to git). Leaving the mismatched `.env` value will break any future `Hermes.Enabled=true` run that loads `.env` via `start-soulcore.ps1`.

## Reproduction Steps

1. Compare key lengths / equality (do not log values).
2. `GET http://127.0.0.1:8642/v1/models` with SoulCore Bearer → 401.
3. Same with Hermes `API_SERVER_KEY` → 200.

## Expected Result

SoulCore Hermes API key matches gateway `API_SERVER_KEY`.

## Actual Result

`keys_equal=False`; SoulCore key unauthorized.

## Impact Scope

Blocks Hermes chat/MCP until env corrected. No secrets committed.

## Suggested Fix (PM → OPS)

Sync `SOULCORE_HERMES_API_KEY` in local gitignored `.env` to Hermes `API_SERVER_KEY` (or document single source of truth). Do not commit the value.
