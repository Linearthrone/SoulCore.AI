# CLEANUP Sweep 3 — verification (+ House leftovers)

Branch: `cursor/prop17-cleanup-sweeps-6372`  
Code: `981012c`  
Prior: SoulCore Sweep 1–2 (`67418cc` / `616d285`); House Sweep 1 (`f36f472` / report `f199b4a`)

## Scope

1. Re-scan SoulCore PROP-17 for anything Sweeps 1–2 missed (excluding documented leave-alones).
2. Finish House PROP-17 surface leftovers after House Sweep 1 landed mid-wave.
3. Run SoulCore Prop17*/TaskTools*/WorkContinuation*/GuestWorkspace*/EmailTools*/Mt4Tools*, House.ChatDesktop.Tests, Android unit tests.

## SoulCore re-scan

No new low-risk actionable slop beyond Sweep 2 leave-alones:

- Parallel `NotingRegistry` types (divergent semantics)
- Email/Mt4 gate overloads (one-line domain flags)
- TaskCreate/Update catch contracts; PlaywrightBrowserBridge mega-surface
- Schema/002+ unused embeddings (Memory ownership)

No SoulCore code changes in this sweep.

## Findings fixed (file:symbol)

| # | Location | Change |
|---|----------|--------|
| 1 | `MainWindow.HumanizeActivity` | Remove dead unused helper |
| 2 | `MainWindow.UpdateEngagementState` | `sleepCat ? "Resting" : "Resting"` → `"Resting"` |
| 3 | `MainWindow.RunServiceActionAsync` | Single `WriteHostActionStatus`; non-host tags no longer stamp HostStack |
| 4 | `LocalStackControl.DefaultHostHealthUrl` | Drop unused constant |
| 5 | `LocalStackControl.GetGitStatusAsync` | Flatten empty `// ok` branch |
| 6 | `LocalStackControl.CreateVBoxPsi` | Share VBoxManage `ProcessStartInfo` for probe + control |
| 7 | `EmailMailboxLabels.SAFE_ID` | Align `{1,64}` with desk `IsSafeMailboxId` |
| 8 | `EmailMailboxLabelsTest` | Assert reject length 65 |
| 9 | `EmailMailboxChoiceTests.IsSafeMailboxId_RejectsOver64` | Lock desk max length |

**Cleanup count: 9**

## FINDINGS_REMAINING

**FINDINGS_REMAINING=no**

Remaining items are intentional leave-alones (cross-language label twins, honest Host-awaiting band/Identity stub copy, Email/Mt4 gate overloads, NotingRegistry divergence, Playwright/Task catch contracts). No concrete safe same-purpose remnant remains.

**CLEANUP_COMPLETE**

## Residual risk

- Low: Host-action status for host tags now shows the same friendly label on Services + HostStack during start (`Starting Host…` instead of Services keeping `Running host-start…`). Non-host lamp actions no longer overwrite HostStackStatusText.
- Low: Android rejects mailbox ids longer than 64 (desk already did). Covered by unit tests.
- Naming: LinearThrone / Kayleigh / Victoria preserved; no Kurt reintroduced.

## Tests

```text
dotnet test SoulCore/SoulCore.Protocol.Tests \
  --filter "FullyQualifiedName~Prop17|FullyQualifiedName~TaskTools|FullyQualifiedName~WorkContinuation|FullyQualifiedName~GuestWorkspace|FullyQualifiedName~EmailTools|FullyQualifiedName~Mt4Tools"

Passed!  Failed: 0, Passed: 125, Skipped: 0, Total: 125
```

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj

Passed!  Failed: 0, Passed: 102, Skipped: 0, Total: 102
```

```text
./gradlew :app:testDebugUnitTest --rerun-tasks
BUILD SUCCESSFUL — Android unit tests: tests=25 failures=0 errors=0
```

Logs: `/opt/cursor/artifacts/sweep3-prop17-tests.log`, `/opt/cursor/artifacts/sweep3-house-chatdesktop-tests.log`, `/opt/cursor/artifacts/sweep3-house-android-tests.log`
