---
type: report
prop_id: PROP-15.27
prop_root: PROP-15-persona-creation-platform
from: FED-01
to: PM-01
priority: P2
status: Pass
created: 2026-10-05
updated: 2026-10-05
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
ticket: docs/agents/tasks/PROP-15.27-PM01-to-FED01.md
slop_report: docs/agents/reports/PROP-15.26-SLOP01-to-PM01.md
finding: F1
---

# PROP-15.27 — Live RepoRoot notice redact — Pass

## Verdict

**Pass (client).** Display-only `FormatPathForDisplay` rewrites `SpecialFolder.UserProfile` → `%USERPROFILE%` for Presence “Starting local stack from …” and Settings progress `Repo: …`. Real `RepoRoot` / cwd / scripts / env loading unchanged.

## Done

1. Added `LocalStackControl.FormatPathForDisplay(string?)` — prefix match with separator boundary; Windows case-insensitive.
2. Wired:
   - `MainWindow.Presence.cs` — system notice uses display helper
   - `LocalStackControl.EnsureStackForChatAsync` — `progress?.Report($"Repo: …")` uses display helper
3. Unit tests: rewrite under profile, exact profile, non-profile unchanged, partial-prefix not rewritten, null/empty.

## Key files

| File | Role |
| --- | --- |
| `House/House.ChatDesktop/Services/LocalStackControl.cs` | Helper + progress `Repo:` display |
| `House/House.ChatDesktop/MainWindow.Presence.cs` | “Starting local stack from …” display |
| `House/House.ChatDesktop.Tests/LocalStackControlTests.cs` | FormatPathForDisplay coverage |

## Evidence

### Build (ChatDesktop)

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release --no-incremental

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.00
```

(First attempt failed while Presence PID 41928 locked `bin/Release`; stopped process and rebuilt clean.)

### Tests

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Release
  --filter "FullyQualifiedName~LocalStackControlTests"

Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 27 ms
```

(Includes 5 new `FormatPathForDisplay_*` facts + prior LocalStackControl tests.)

## Notes for PM

- Ready for QA-01 if desired (live Presence auto-start notice should show `%USERPROFILE%\…` not `C:\Users\<profile>\…`).
- Self-claim: **Pass (client)** only — not QA Pass.
