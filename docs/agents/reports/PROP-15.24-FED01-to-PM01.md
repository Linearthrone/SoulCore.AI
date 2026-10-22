---
type: report
prop_id: PROP-15.24
prop_root: PROP-15-persona-creation-platform
from: FED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
ticket: docs/agents/tasks/PROP-15.24-PM01-to-FED01.md
slop_report: docs/agents/reports/PROP-15.23-SLOP01-to-PM01.md
findings: F1, F2, F3
pm_decision_f2: wire-capabilities-personas
---

# PROP-15.24 — SLOP 15.23 cleanup — Pass

## Verdict

**Pass (client).** F1 AGENTS path scrub, F2 `capabilities.personas` wired into Host outdated check (any fail → outdated), F3 feed API URL derived from repo URL. Naming scrub clean under `House/House.ChatDesktop` (+ Tests).

## Done

### F1 — Scrub forbidden path token
- `MainWindow.Presence.cs` system notice example → `%USERPROFILE%\Soul_Core` (no machine username).
- Bonus AGENTS scrub in tests: `CompanionTokenEnvTests` path → `C:\Users\LinearThrone\…`; `PresenceHonestyTests` Victoria-speech fixtures → Kayleigh.

### F2 — Wire `capabilities.personas` (PM decided)
- `SoulCoreHealthClient` parses `/health` `capabilities.personas` → `HealthCapabilitiesPersonas` (`true` only when flag is true; missing/false → false).
- Presence sets `_healthCapabilitiesPersonas` on alive health (null when unreachable).
- `BuildCurrencyStatus.IsHostOutdatedForPersonas` / label / short: third signal; **any** of API-404, missing persona object, or capabilities missing/false → Host outdated. Existing checks retained.

### F3 — Single feed URL owner
- `PresenceUpdateService.DefaultGithubApiReleasesUrl` is `static readonly`, derived via `TryGithubApiFromRepoUrl(DefaultGithubRepoUrl)` — no parallel hardcoded owner/repo string.

## Key files

| File | Role |
| --- | --- |
| `House/House.ChatDesktop/MainWindow.Presence.cs` | F1 path scrub; F2 health → capabilities flag |
| `House/House.ChatDesktop/MainWindow.Updates.cs` | F2 chrome wiring |
| `House/House.ChatDesktop/Services/SoulCoreHealthClient.cs` | Parse `capabilities.personas` |
| `House/House.ChatDesktop/Services/BuildCurrencyStatus.cs` | Triple-signal outdated check |
| `House/House.ChatDesktop/Services/PresenceUpdateService.cs` | F3 derive API URL |
| `House/House.ChatDesktop.Tests/BuildCurrencyStatusTests.cs` | Any-fail outdated cases |
| `House/House.ChatDesktop.Tests/PresenceUpdateServiceTests.cs` | API URL derived from repo |
| `House/House.ChatDesktop.Tests/PresenceHonestyTests.cs` | AGENTS Kayleigh fixtures |
| `House/House.ChatDesktop.Tests/CompanionTokenEnvTests.cs` | AGENTS LinearThrone path |

## Evidence

### Build (ChatDesktop)

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release --no-incremental

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.10
```

(Note: first attempt failed while Presence PID 16140 locked `bin/Release`; stopped process and rebuilt clean.)

### Tests

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Release
  --filter "FullyQualifiedName~BuildCurrencyStatus|FullyQualifiedName~PresenceUpdateService|FullyQualifiedName~PresenceHonesty|FullyQualifiedName~CompanionTokenEnvTests.EnumerateEnvFileCandidates"

Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43, Duration: 23 ms
```

### Naming scrub

```text
git grep -i -E '<forbidden AGENTS human-name tokens>' -- House/House.ChatDesktop House/House.ChatDesktop.Tests
# exit 1 — no matches (ACCEPTANCE clean)
```

### Acceptance checklist

- [x] No forbidden AGENTS human-name tokens in ChatDesktop scoped paths (`git grep` clean)
- [x] Currency uses `capabilities.personas` (alongside persona object + API 404)
- [x] Single feed URL owner (`DefaultGithubApiReleasesUrl` derived)
- [x] Tests + report `docs/agents/reports/PROP-15.24-FED01-to-PM01.md`

## Notes for PM

- Pre-existing unrelated fail if full filter includes `CompanionTokenEnvTests.TryLoadFromEnvFile_UsesConfiguredRepoRootOutsideBaseDirectory` (settings token wins over env file on this machine) — not introduced by 15.24; not in acceptance filter above.
- Ready for QA spot-check if desired; functional 15.22b Pass stands — this was hygiene + signal wiring only.
