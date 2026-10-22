---
type: report
prop_id: PROP-15.27b
prop_root: PROP-15-persona-creation-platform
from: QA-01
to: PM-01
priority: P2
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.27 (Pass)
fed_report: docs/agents/reports/PROP-15.27-FED01-to-PM01.md
ticket: docs/agents/tasks/PROP-15.27b-PM01-to-QA01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Smoke — RepoRoot display redact in Presence/Settings notices
---

# PROP-15.27b — RepoRoot display redact smoke after 15.27 — **Pass**

## Verdict: **Pass**

Live smoke on CreatorEdition **Release** ChatDesktop (rebuilt from this tree with 15.27 bits). New Presence system notice shows **`Starting local stack from %USERPROFILE%\Soulcore.AI\SoulCore.AI.`** — no machine-username path segment on the new emit. Stack still resolves the real RepoRoot under the hood (`Stack already running` + Host `/health` OK). Currency chrome **Personas OK** with Host **0.2.0**. Product code untouched. **Ready for SLOP-01**.

## Environment

| Check | Result |
| --- | --- |
| Host | `127.0.0.1:7700` — `version=0.2.0`, `capabilities.personas=true`, `status=ok` (feel-drive pin preserved; not killed) |
| ChatDesktop | `dotnet build House/House.ChatDesktop -c Release --no-incremental` — **0 Warning(s), 0 Error(s)**. Ran unpackaged `bin/Release/net8.0/House.ChatDesktop.exe` Presence **0.1.8** (PID **17072** after clean relaunch) |
| Method | Windows UI Automation + Host REST + `LocalStackControl` / `BuildCurrencyStatus` unit tests. No product code changes |
| Repo under test | Settings-configured checkout under profile (`…\Soulcore.AI\SoulCore.AI`) — not a rename of cwd; functional ensure path exercised |

## Acceptance checklist

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Trigger local-stack start so RepoRoot notice/progress appears | **Pass** | Auto-start on Presence open emitted “Starting local stack from …” then “Stack already running …” (Host already up) |
| 2 | User-visible string uses `%USERPROFILE%\…`, not `C:\Users\<machine-username>\…` | **Pass** | Fresh emit (UIA): `Starting local stack from %USERPROFILE%\Soulcore.AI\SoulCore.AI.` Unit tests: `LocalStackControl` + currency filter **31 passed** (includes `FormatPathForDisplay_*`) |
| 3 | Stack still starts / resolves real path (not a cwd rename) | **Pass** | Same session: `Stack already running` + Host remained `0.2.0` / `personas=true` / active **Alexia**. Display helper is emit-only; real `RepoRoot` unchanged per FED 15.27 |
| 4 | Currency chrome still `Personas OK` if Host 0.2.0 up | **Pass** | `BuildVersionsText` = `Presence 0.1.8 · Unpackaged · Host 0.2.0 · Personas OK` |

## Live evidence (condensed)

| Step | Result |
| --- | --- |
| Release rebuild | ChatDesktop Release `--no-incremental` succeeded (0/0) |
| Unit tests | `LocalStackControlTests` + `BuildCurrencyStatus` — **31 passed** |
| New Presence notice | `Starting local stack from %USERPROFILE%\Soulcore.AI\SoulCore.AI.` |
| Stack functional | `Stack already running` · Host `/health` ok · personas true |
| Currency | `Personas OK` |

## Notes (non-blocking)

1. **Persisted transcript history:** Pre-15.27 “Starting local stack from `C:\Users\<machine-username>\…`” lines remain in the chat transcript (stored text). New emits are redacted. Retroactive scrub of history is **out of scope** for 15.27 / 15.27b unless PM tickets it.
2. **Settings `Repo:` progress:** `EnsureStackForChatAsync` reports `Repo: {FormatPathForDisplay(RepoRoot)}` then quickly overwrites with `Ollama already up` / `Host already up`, so `ServicesStatusText` did not retain `Repo:` at UIA sample time. Same helper covered by unit tests + Presence notice live proof.

## Skip / constraints honored

- No product code changes
- No Host kill (feel-drive pin preserved)
- Naming: LinearThrone / Kayleigh rules observed in this report (no forbidden human-name tokens)

## Ask PM

1. Accept **Pass** for PROP-15.27b.
2. Dispatch **SLOP-01** (post-QA slop audit) on the 15.27 client redact surface.
3. Optional: ticket FED if persisted pre-redact transcript lines should be rewritten or filtered on render.
