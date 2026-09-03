---
type: issue
id: ISSUE-20260722-001
severity: P0
status: closed
created: 2026-07-22
updated: 2026-07-23
closed_note: User rotated 2026-07-23; BED-054 Host DotEnvLoader + OPS-055 start script load SOULCORE_* from SoulCore/.env.
source: docs/agents/reports/TASK-20260722-004-SEC01-to-PM01.md
ops_report: docs/agents/reports/TASK-20260722-008-OPS01-to-PM01.md
---

# ISSUE-001 · Live A2E API token committed in LLMOD git

## Summary

Live A2E API token present in git-tracked quarry files. Treat as compromised if the repo was ever pushed/shared.

## Evidence

- `C:\Users\kurtw\LLMOD\LLMOD-max-master\HouseVictoria.App\App.config` (A2eApiToken)
- `C:\Users\kurtw\LLMOD\LLMOD-max-master\tmpcode\build-out-notify\HouseVictoria.App.dll.config` (copy)

SEC-004 report: `docs/agents/reports/TASK-20260722-004-SEC01-to-PM01.md`

## Impact

Token usable by anyone with repo access. Blocks safe public/remote exposure of LLMOD. Does **not** block SoulCore Phase 0 scaffold if secrets are not copied.

## Required actions (owner / OPS)

1. Rotate A2E token at provider immediately — **DONE (user confirmed 2026-07-23)**
2. Scrub working tree values → env/user-secrets placeholders — **DONE (OPS-008, staged)**
3. Stop tracking `tmpcode/**` and `*.dll.config`; fix quarry `.gitignore` (also `.env`) — **DONE (OPS-008)**
4. Consider history purge if remote ever contained the token — recommended, not executed
5. SoulCore `.env` auto-load — **DONE** (BED-054 Host; OPS-055 start script)

## Related

Also P1/P2: HF token on disk; tracked `MCPServer/.env`; Hermes key in App.config — see SEC-004 table.
