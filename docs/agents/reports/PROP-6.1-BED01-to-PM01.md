---
type: report
prop_id: PROP-6.1
from: BED-01
to: PM-01
status: Completed
branch: cursor/prop6-desktop-delay-8a1f
created: 2026-09-05
---

# PROP-6.1 — Desktop drag async delay (BED-01 → PM-01)

## Verdict

**Completed** — `NativeDesktopControlBackend` drag interpolation no longer blocks a thread via `Thread.Sleep`; delays are cancellable via `Task.Delay(..., ct)`.

## Branch

`cursor/prop6-desktop-delay-8a1f` (based on `main` to avoid PROP-5 Memory WIP compile breakage on `cursor/tina-wave-prop5-11-8a1f`).

## Before / After

| Item | Before | After |
| --- | --- | --- |
| Delay primitive | `Thread.Sleep(15)` inside `Task`-returning `DragAsync` | `await Task.Delay(15, ct).ConfigureAwait(false)` |
| Method shape | Sync body wrapped in `Task.FromResult` | `async Task<DesktopOpResult>` |
| Cancellation | `ct.ThrowIfCancellationRequested()` in loop only; swallowed by generic catch | Checked before OS guard; `OperationCanceledException` rethrown |
| Thread pin (~300 ms) | Yes (20 × 15 ms) | No — thread yields during delay |

### Key diff (interpolation loop)

```diff
-                Thread.Sleep(15);
+                await Task.Delay(15, ct).ConfigureAwait(false);
```

## Files touched (fence respected)

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Inference/Tools/Desktop/NativeDesktopControlBackend.cs` | Sleep → Delay; async DragAsync |
| `SoulCore/SoulCore.Protocol.Tests/NativeDesktopControlBackendTests.cs` | New regression + behavior tests |

No edits to Host, Memory, Charter, Hermes, or `Program.cs`.

## Evidence

### `rg Thread.Sleep` (target file)

```text
$ rg Thread.Sleep SoulCore/SoulCore.Inference/Tools/Desktop/NativeDesktopControlBackend.cs
(no matches — exit 1)
```

### Tests

```text
$ dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
    --filter "FullyQualifiedName~NativeDesktopControlBackendTests"

Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5

$ dotnet test ... --filter "FullyQualifiedName~Desktop"

Passed!  - Failed: 0, Passed: 148, Skipped: 0, Total: 148
```

New tests:

1. `DragAsync_SourceHasNoThreadSleep` — source regression
2. `DragAsync_SourceUsesCancellableTaskDelay` — confirms `await Task.Delay(15, ct)`
3. `DragAsync_HonorsCancellationBeforeStart` — cancelled token throws `OperationCanceledException`
4. `DragAsync_OnNonWindows_CompletesWithoutBlocking` — Linux CI fast path (< 100 ms)
5. `DragAsync_ReturnsCompletedTaskWithoutSyncBlock_OnNonWindows` — task completes without 300 ms sync block

## Acceptance checklist

| # | Criterion | Result |
| --- | --- | --- |
| 1 | Zero `Thread.Sleep` in `NativeDesktopControlBackend.cs` | ✅ |
| 2 | Delays cancellable | ✅ |
| 3 | Desktop tool tests pass | ✅ (148/148) |
| 4 | Report shows before/after + test output | ✅ |

## Notes

- Mid-drag cancellation leaves the cursor at the last interpolated position (documented in proposal; prior moves retained, no rollback).
- Windows foreground drag timing not exercised in Linux CI; covered by source regression + async pattern. Windows validation recommended on Kurt's host.
