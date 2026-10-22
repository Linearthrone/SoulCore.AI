---
type: report
prop_id: PROP-15.28
prop_root: PROP-15-persona-creation-platform
from: SLOP-01
to: PM-01
task_id: PROP-15.28
status: clean
created: 2026-10-05
updated: 2026-10-05
qa_report: docs/agents/reports/PROP-15.27b-QA01-to-PM01.md
fed_report: docs/agents/reports/PROP-15.27-FED01-to-PM01.md
prior_slop: docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
---

# SLOP-01 Report — PROP-15.28

## Scope
- Related QA task / report: `PROP-15.27b` / `docs/agents/reports/PROP-15.27b-QA01-to-PM01.md` (Pass)
- Related DEV: `PROP-15.27` (FED — `FormatPathForDisplay` for Presence notice + Settings `Repo:` progress)
- Prior slop: `PROP-15.26` F1 (live RepoRoot raw interpolation — should be closed)
- Files / packages scanned:
  - `House/House.ChatDesktop/Services/LocalStackControl.cs` (`FormatPathForDisplay` + `EnsureStackForChatAsync` progress)
  - `House/House.ChatDesktop/MainWindow.Presence.cs` (Presence “Starting local stack from …” notice)
  - `House/House.ChatDesktop.Tests/LocalStackControlTests.cs` (`FormatPathForDisplay_*`)
  - Ripgrep across `House/House.ChatDesktop` for `FormatPathForDisplay`, `RepoRoot` interpolations, `%USERPROFILE%`, `Starting local stack`, `Repo:`
- Method: read + ripgrep. No product code modified. Host / currency / stress suite out of scope per ticket.

## Summary
- Findings count: **0**
- Highest severity: n/a
- Prior 15.26 F1: **closed**
- Naming (AGENTS) on scoped display surfaces: **clean** (redact helper wired; no leftover raw-RepoRoot notice/progress interpolations)

## Prior finding re-check (15.26 F1)

| ID | Status | Evidence |
| --- | --- | --- |
| F1 live RepoRoot notice embeds machine username path | **Closed** | Presence notice uses `LocalStackControl.FormatPathForDisplay(_stack.RepoRoot)` (`MainWindow.Presence.cs` ~L164). Settings progress uses `FormatPathForDisplay(RepoRoot)` (`LocalStackControl.cs` ~L166). Grep: no remaining `$"…{RepoRoot}…"` / `$"Repo: {RepoRoot}"` on those two user-visible surfaces. Helper rewrites `SpecialFolder.UserProfile` → `%USERPROFILE%` with separator-boundary + Windows case-insensitive match (`LocalStackControl.cs` ~L372–401). QA 15.27b live: `%USERPROFILE%\Soulcore.AI\SoulCore.AI.` |

## Findings

None.

## Clean areas
- **Single helper, both call sites:** one `FormatPathForDisplay`; Presence notice + Settings `Repo:` progress both call it — no alias sprawl / parallel redact helpers.
- **Display vs functional paths:** real `RepoRoot` still used for cwd / scripts / env (`Set-Location`, `Path.Combine`, companion token) — correct; no accidental cwd rename via display helper.
- **Tests:** five focused `FormatPathForDisplay_*` facts (under-profile rewrite, exact profile, non-profile unchanged, partial-prefix guard, null/empty) — match helper behavior; no duplicate test helpers.
- **XML doc on helper:** states display-only intent; appropriate, not restating noise.
- **Example copy already redacted:** missing-repo notice uses `%USERPROFILE%\Soul_Core` (~L158–160) — consistent with closed 15.23 path-scrub pattern.
- **Settings TextBox raw path:** editable `SoulCoreRepoRootBox` still shows real path for configuration (watermark `C:\Users\you\Soul_Core`) — intentional edit surface, not a notice/progress string; out of ticket scope and not flagged.

## Notes for PM
- Safe to archive 15.27 / 15.27b / 15.28 chain? **yes** — 15.26 F1 closed; no actionable slop/dupes on the redact surface.
- Non-blocking (already in QA 15.27b notes): persisted pre-15.27 transcript lines may still contain raw profile paths; new emits are redacted. Retroactive scrub only if PM tickets it.
- Product code untouched by SLOP-01.
