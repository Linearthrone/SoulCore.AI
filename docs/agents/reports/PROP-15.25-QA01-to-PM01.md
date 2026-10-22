---
type: report
prop_id: PROP-15.25
prop_root: PROP-15-persona-creation-platform
from: QA-01
to: PM-01
priority: P1
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.24 (Pass)
fed_report: docs/agents/reports/PROP-15.24-FED01-to-PM01.md
ticket: docs/agents/tasks/PROP-15.25-PM01-to-QA01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Smoke — capabilities.personas currency + naming scrub after 15.24
---

# PROP-15.25 — Currency + naming smoke after 15.24 — **Pass**

## Verdict: **Pass**

Live smoke on CreatorEdition **Release** ChatDesktop (rebuilt from this tree with 15.24 bits) against Host **0.2.0** (`capabilities.personas=true`). Title chrome shows **Personas OK** (not Host outdated). Switcher DisplayNames + Mentor quarantine activate + Alexia restore OK. F1 example path uses `%USERPROFILE%\Soul_Core`. Product code untouched. **Ready for SLOP-01**.

## Environment

| Check | Result |
| --- | --- |
| Host | `127.0.0.1:7700` — `version=0.2.0`, `capabilities.personas=true`, `GET /api/personas` **200** (6 packs), start/end pin **lexi / Alexia** + `mother:latest` / `modelSource=persona` |
| ChatDesktop | Rebuilt Release `House/House.ChatDesktop` (`dotnet build -c Release --no-incremental`, 0 errors). Ran unpackaged `bin/Release/net8.0/House.ChatDesktop.exe` Presence **0.1.8** (PID **41928**) — not a stale packaged Presence |
| Method | Host REST + Windows UI Automation; currency unit tests; source spot for F1 path. No product code changes |
| Optional Host-outdated live | **Skipped** — left feel-drive Host pin up. Outdated/capabilities-false covered by `BuildCurrencyStatus` unit tests (21 passed) |

## Acceptance checklist

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Title bar / Settings → Updates: Host personas OK, not outdated, Host 0.2.0 | **Pass** | `BuildVersionsText` = `Presence 0.1.8 · Unpackaged · Host 0.2.0 · Personas OK` (window title `SoulCore CreatorEdition - Presence 0.1.8`). Avalonia Settings → Updates tab fields not exposed cleanly via UIA on this seat (TabItems surfaced as type names; boxes null) — same chrome string is owned by `RefreshBuildVersionChrome` / `BuildCurrencyStatus` for both title and Updates boxes |
| 2 | Switcher DisplayNames; one quarantine activate + restore | **Pass** | ListItems: **Analyst**, **blank**, **Aurora Desk**, **Alexia**, **Mentor**, **Victoria**. Activate **Mentor** → Host `mentor` / UI Identity **Mentor** / model `gemma4:latest` (`source=host`). Restore **Alexia** → Host `lexi` / `mother:latest` / `modelSource=persona`. ↻ → `6 pack(s) · active: Alexia`. Chrome stayed **Personas OK** throughout |
| 3 | Naming: no forbidden AGENTS human-name tokens in Presence path *examples* | **Pass** (F1) | Source notice example: `%USERPROFILE%\Soul_Core`. Grep of `House/House.ChatDesktop` + Tests `*.cs`/`*.axaml` — no forbidden name tokens. **Finding (non-blocking / pre-existing):** live “Starting local stack from {RepoRoot}…” system notices interpolate the real disk path and currently show a `C:\Users\<machine-username>\…` segment in the transcript — outside the 15.24 F1 example scrub; recommend separate FED to present repo root without embedding the machine username |

## Live evidence (condensed)

| Step | Result |
| --- | --- |
| `GET /health` | `0.2.0`, `capabilities.personas=true`, persona object present |
| `GET /api/personas` | 200, `activePersonaId=lexi` (end state) |
| Title chrome | `Presence 0.1.8 · Unpackaged · Host 0.2.0 · Personas OK` |
| Switcher | 6 DisplayNames (not id-only) |
| Activate Mentor | Host + UI switched; chrome still Personas OK |
| Restore Alexia | Host + UI restored; pin/model restored |
| BuildCurrencyStatus tests | 21 passed (includes capabilities.personas any-fail → outdated) |

## Skip / constraints honored

- No product code changes
- No Host kill / no long outdated Host experiment (feel-drive pin preserved)
- Naming rules observed in this report (LinearThrone / Kayleigh only where required)

## Ask PM

1. Accept **Pass** for PROP-15.25.
2. Dispatch **SLOP-01** (post-QA slop audit) on the 15.24 client surface.
3. Optional: ticket FED to stop interpolating raw `RepoRoot` with machine-username path segments into Presence system notices.
