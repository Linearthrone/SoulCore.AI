---
type: issue
id: ISSUE-009
severity: P1
status: Fixed
created: 2026-07-27
filed_by: QA-01
fixed_by: BED-01
related_task: TASK-145, TASK-161
gate: QA-145
updated: 2026-07-27 (Fixed by BED-161: Example/.env notes + Host startup preflight; no secrets in git)
---

# ISSUE-009 — Hermes API key docs / preflight misaligned

## Severity

**P1 — Operator footgun.** PreferHermes / hermes-backend MCP tools require `SOULCORE_HERMES_API_KEY`, but docs and startup did not consistently warn when Hermes was enabled without a key. Risk of putting keys in `appsettings*.json` (forbidden).

## Fix (BED-161)

- `appsettings.Example.json` PreferHermes note documents fail-fast + CallMcpToolAsync key need.
- `SoulCore/.env.example` documents PreferHermes + key + gateway requirements.
- Host startup preflight warns when `Hermes.Enabled` or `PreferHermes` is set without env/user-secrets key; warns if `Hermes:ApiKey` is bound from config.
- `--secrets-presence` already reports `SOULCORE_HERMES_API_KEY` present/length (no value).
- **No secrets committed.**

## Status

**Fixed**
