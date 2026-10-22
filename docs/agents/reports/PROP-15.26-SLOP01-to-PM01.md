---
type: report
prop_id: PROP-15.26
prop_root: PROP-15-persona-creation-platform
from: SLOP-01
to: PM-01
task_id: PROP-15.26
status: findings
created: 2026-10-05
updated: 2026-10-05
qa_report: docs/agents/reports/PROP-15.25-QA01-to-PM01.md
fed_report: docs/agents/reports/PROP-15.24-FED01-to-PM01.md
prior_slop: docs/agents/reports/PROP-15.23-SLOP01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
---

# SLOP-01 Report — PROP-15.26

## Scope
- Related QA task / report: `PROP-15.25` / `docs/agents/reports/PROP-15.25-QA01-to-PM01.md` (Pass)
- Related DEV: `PROP-15.24` (FED — capabilities wiring + feed/naming scrub)
- Prior slop: `PROP-15.23` F1–F3 (closed by 15.24; re-checked for regressions only)
- Files / packages scanned:
  - `House/House.ChatDesktop/Services/SoulCoreHealthClient.cs` (`HealthCapabilitiesPersonas` parse)
  - `House/House.ChatDesktop/Services/BuildCurrencyStatus.cs` (triple-signal outdated)
  - `House/House.ChatDesktop/MainWindow.Presence.cs` (F1 example path; health → capabilities flag; RepoRoot notice)
  - `House/House.ChatDesktop/MainWindow.Updates.cs` (chrome wiring)
  - `House/House.ChatDesktop/Services/PresenceUpdateService.cs` (`DefaultGithubApiReleasesUrl` derive)
  - Related: `LocalStackControl.cs` (Repo progress string — sibling of QA notice note)
  - Tests: `BuildCurrencyStatusTests`, `PresenceUpdateServiceTests`, `PresenceHonestyTests`, `CompanionTokenEnvTests`
- Method: read + ripgrep (capabilities/feed/RepoRoot/AGENTS naming). No product code modified.

## Summary
- Findings count: **1** (1× P2)
- Highest severity: **P2**
- Prior 15.23 F1–F3: **closed, no regressions**
- Naming (AGENTS) in scoped `*.cs`/`*.axaml` source: **clean** (no forbidden human-name tokens)

## Prior findings re-check (15.23)

| ID | Status | Evidence |
| --- | --- | --- |
| F1 path scrub | **Closed** | `MainWindow.Presence.cs` ~L158–160 example is `%USERPROFILE%\Soul_Core`. Grep of ChatDesktop + Tests: no forbidden AGENTS human-name tokens. Tests use `LinearThrone` / `Kayleigh` where required. |
| F2 capabilities wire | **Closed** | `SoulCoreHealthClient` → `HealthCapabilitiesPersonas`; Presence sets `_healthCapabilitiesPersonas`; `BuildCurrencyStatus.IsHostOutdatedForPersonas` / label / short take third signal (any fail → outdated). Theory coverage in `BuildCurrencyStatusTests`. |
| F3 feed URL dedupe | **Closed** | `DefaultGithubApiReleasesUrl` is `static readonly` from `TryGithubApiFromRepoUrl(DefaultGithubRepoUrl)`. No parallel hardcoded API owner/repo. Test `DefaultGithubApiReleasesUrl_DerivedFromDefaultRepoUrl` asserts equality. |

## Findings

### F1 — Live RepoRoot notice embeds machine username path segment
- Category: slop
- Severity: P2
- Evidence:
  - `MainWindow.Presence.cs` ~L164 — `AppendSystemNotice($"Starting local stack from {_stack.RepoRoot}…")` interpolates the real disk path (QA live transcript showed `C:\Users\<machine-username>\…`)
  - Sibling (same pattern): `LocalStackControl.cs` ~L166 — `progress?.Report($"Repo: {RepoRoot}")` (Settings/status line, not chat transcript)
  - Outside the closed 15.23/15.24 *example* scrub; Settings watermark already neutral (`C:\Users\you\Soul_Core`)
- Why it matters: AGENTS zero-exception naming — user-visible Presence copy must not surface forbidden human-name tokens when the Windows profile folder is that name. Pre-existing; non-blocking per QA 15.25.
- Recommended action: **ticket-FED**
- Suggested owner: FED-01
- Minimal fix (do not redesign): when formatting user-visible notices/progress, if `RepoRoot` starts with `Environment.GetFolderPath(SpecialFolder.UserProfile)`, rewrite that prefix to `%USERPROFILE%` (or show only the leaf folder name). Keep real paths for process cwd / script execution unchanged.
- Ask-user question: n/a — disposition is clear and small.

## Clean areas
- **Triple-signal currency**: intentional Host answers (API 404 + persona object + `capabilities.personas`); shared `IsHostOutdatedForPersonas` — not alias sprawl after PM wire decision.
- **`HostCurrencyLabel` / `HostCurrencyShort`**: intentional long vs short surfaces; shared outdated gate — unchanged pattern, not actionable.
- **Feed defaults**: single repo URL owner; API URL derived; comment in `LocalUiSettings` matches CreatorEdition feed.
- **15.24 comments / XML**: brief, match local style; no dead wrappers or unused exports in the change surface.
- Stress-suite / Host product trees left out of scope per ticket.

## Notes for PM
- Safe to archive 15.24 hygiene slice? **yes for F1–F3 closure** — optional follow-up only for this report’s **F1** (live RepoRoot redact).
- Functional QA Pass (15.25) stands; finding is naming hygiene on a pre-existing notice, not reopen of capabilities/chrome/feed work.
- Prefer a small FED ticket over defer if Presence transcripts on LinearThrone’s machine must stay AGENTS-clean in live runs.
