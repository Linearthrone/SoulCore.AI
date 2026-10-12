# CLEANUP Sweep 1 — House PROP-17

Branch: `cursor/prop17-cleanup-sweeps-6372`  
Commits: `f36f472` + follow-up code; this report  
Scope: `House/House.ChatDesktop/**`, `House/House.CompanionAndroid/**` (PROP-17 shell, Host Stop, PresenceHud, email labels). No `SoulCore/**`.

## Findings fixed (file:symbol)

| # | Location | Change |
|---|----------|--------|
| 1 | `HostStackBusyPolicy` | Drop trivial `IsHostStartEnabled` / `IsHostRestartEnabled` (`!busy` wrappers) |
| 2 | `HostStackBusyPolicyTests` | Keep Stop-policy coverage only |
| 3 | `MainWindow.SetHostStackButtonsBusy` | Enable Start/Restart with `!busy`; Stop via policy |
| 4 | `MainWindow.WriteHostActionStatus` | Dedupe dual Services/HostStack status writes |
| 5 | `MainWindow.InterruptOrStopHostAsync` | Narrow cancel catch to `ObjectDisposedException` |
| 6 | `MainWindow.DisposeHostActionCts` | Same disposed catch (no bare `catch`) |
| 7 | `EmailMailboxChoice.IsSafeMailboxId` | Shared id rules; remove private copy in `EmailSetupWindow` |
| 8 | `EmailMailboxChoice.DefaultDisplayNameFor` | Merge `personal`/`business` → LinearThrone |
| 9 | `EmailMailboxChoice` / `EmailSetupWindow` | Trim restating PROP-17 narrative comments |
| 10 | `LocalStackControl` sandbox + cancel docs | Ticket-number comment noise → behavioral notes |
| 11 | `MainWindow.Presence` repo notice | `kurtw` path → `LinearThrone` |
| 12 | `MainWindow.RefreshActivityBand` | Flatten stub assignments; keep honest copy |
| 13 | `MainWindow.axaml.cs` `_hostActionGeneration` | Comment without ticket prefix |
| 14 | `PresenceHonestyTests` | Kurt → Kayleigh in fixtures |
| 15 | `CompanionTokenEnvTests` | `kurtw` sample path → LinearThrone |
| 16 | `EmailMailboxChoiceTests` | Cover `IsSafeMailboxId` (+ over-64) |
| 17 | `EmailMailboxLabels` / `PresenceHud` / HUD call sites | Trim PROP-17 restating comments |
| 18 | `HealthClient.currentActivity` | Trim ticket prefix on doc |
| 19 | `MainWindow.HumanizeActivity` | Remove unused dead helper from Presence honesty churn |
| 20 | `LocalStackControl.CreateVBoxPsi` | Dedupe VBoxManage ProcessStartInfo builders |
| 21 | `LocalStackControl.DefaultHostHealthUrl` | Remove unused constant |
| 22 | `LocalStackControl.GetGitStatusAsync` | Flatten empty rev-list success branch |
| 23 | `EmailMailboxLabels.SAFE_ID` | Align Android max length 64 with desk |

**Cleanup count: 23**.

## Deferred (Sweep 2+)

- Parallel C# `EmailMailboxChoice` vs Kotlin `EmailMailboxLabels` (same product rules, different runtimes — no safe share without codegen).
- `SubtitleFor` vs `FormatSystemListLine` state wording (intentional UI surfaces).
- Activity-band stub strings still mention awaiting PROP-17.1/17.3 Host APIs (product honesty, not dead code).
- `RunServiceActionAsync` large switch — extract only with Host Stop regression coverage.
- Broader `MainWindow.axaml` PROP-17 markup comments (low value vs noise).

## Residual risk

- Low: `WriteHostActionStatus` preserves prior Services vs HostStack write rules (host tags only update HostStack on completion; start always updates both).
- Low: `IsSafeMailboxId` regex matches prior desk 1–64 charset rules; Android now rejects >64 (aligned).
- Naming: Kurt/`kurtw` removed from House ChatDesktop + CompanionAndroid + their tests; LinearThrone / Kayleigh / Victoria retained.

## Tests

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj
Passed!  Failed: 0, Passed: 102, Skipped: 0, Total: 102
```

```text
./gradlew :app:testDebugUnitTest --rerun-tasks
BUILD SUCCESSFUL — Android unit tests: tests=25 failures=0 errors=0
(includes EmailMailboxLabelsTest, PresenceHudTest, HealthClientActivityTest)
```

Logs: `/opt/cursor/artifacts/sweep1-house-chatdesktop-tests.log`, `/opt/cursor/artifacts/sweep1-house-android-tests.log`

## Sweep 2 recommendation

**No.** Safe House PROP-17 deslop is largely done; remaining work is either cross-language label twins, Host-awaiting stub copy, or Stop-path structure that needs dedicated regression before further reshape.
