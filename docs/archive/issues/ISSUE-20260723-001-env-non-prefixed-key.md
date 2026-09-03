---
type: issue
issue_id: "20260723-001"
severity: P3
status: closed
created: 2026-07-23
filed_by: QA-01
task_ref: TASK-20260723-081
closed_note: PM fixed 2026-07-23 — converted alias line to a comment in SoulCore/.env. Re-audit on next secrets scan.
---

# ISSUE-20260723-001 — .env non-prefixed key convention deviation

## Summary

During the TASK-081 secrets audit, a non-`SOULCORE_*` prefixed key was found in `SoulCore/.env`:

```
CUSTOM_API_KEY == SOULCORE_HERMES_API_KEY
```

This is **not a leaked secret** — it is an alias/reference note (not a `KEY=value` assignment), and no source code references `CUSTOM_API_KEY`. The three real secrets (`SOULCORE_A2E_TOKEN`, `SOULCORE_HERMES_API_KEY`, `SOULCORE_HF_TOKEN`) are correctly prefixed and correctly excluded from version control.

## Severity rationale

P3 — no security impact. The `.env` is not git-tracked (the workspace has a `.gitignore` stub but no `.git` repo currently). No secret value is exposed. The deviation is purely a convention hygiene matter: the `.env` file should contain only `SOULCORE_*`-prefixed assignments per the documented loader contract.

## Evidence

- File: `SoulCore/.env` line 4
- Grep for `CUSTOM_API_KEY` across `SoulCore/`: only 1 hit, in `.env` (no source reference)
- Grep for real-token patterns in build output (`bin/`): 0 matches

## Recommended fix

- Remove the `CUSTOM_API_KEY == SOULCORE_HERMES_API_KEY` line from `SoulCore/.env`, OR
- Convert to a documented comment: `# CUSTOM_API_KEY is an alias for SOULCORE_HERMES_API_KEY`
- Ensure any future custom keys use the `SOULCORE_*` prefix to match the Host loader contract.

## QA verification

No regression test required. Re-audit on next secrets scan (next continuity re-run).
