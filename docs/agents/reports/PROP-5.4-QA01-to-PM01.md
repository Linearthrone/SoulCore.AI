---
type: report
prop_id: PROP-5.4
from: QA-01
to: PM-01
status: Completed
created: 2026-09-05
branch: cursor/prop5-sqlite-gate-8a1f
verdict: Pass
---

# PROP-5.4 — QA-01 concurrent soak report

## Verdict: **Pass**

PROP-5 gates hold under concurrent memory + charter + SoulLoop overlap. No SQLite concurrency exceptions observed. SoulLoop single-flight behavior confirmed. Host boots on Linux cloud VM; `/health` stayed `200`/`status=ok`/`memory.open=true` for 30 probes while unit soak ran in parallel.

## Acceptance criteria

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Soak: no Sqlite concurrency exceptions | **Pass** | `Prop54ConcurrentSoakTests` (480 parallel ops) + `SqlitePathGateConcurrencyTests` (200-iteration soak); zero exceptions in `ConcurrentBag` |
| 2 | Single-flight tick behavior observed | **Pass** | `SoulLoopScaffoldSingleFlightTests` (2 tests) + `Prop54ConcurrentSoakTests.SoulLoop_OverlappingTicksDuringDbStorm_*` (`GetCallCount` in 1–4 under 24 overlapping ticks) |
| 3 | Health remains available under DB load | **Pass** | `soak-prop54-concurrent.sh`: 30/30 `/health` probes OK while dotnet soak ran |
| 4 | Protocol.Tests green (PROP-5 scope) | **Pass** | Focused filter 32/32; full suite 677/678 (1 pre-existing Linux symlink test unrelated to PROP-5) |

## Commands run

```bash
git checkout cursor/prop5-sqlite-gate-8a1f && git pull origin cursor/prop5-sqlite-gate-8a1f

# PROP-5 focused gate + soak tests
cd SoulCore
dotnet test SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
  --filter "FullyQualifiedName~CharterService|FullyQualifiedName~SqliteMemory|FullyQualifiedName~SoulLoopScaffold|FullyQualifiedName~SqlitePathGate|FullyQualifiedName~Prop54" \
  --verbosity minimal
# → Passed: 32, Failed: 0

# Host boot (Linux cloud — inference/unreal disabled for smoke)
SOULCORE_Hermes__Enabled=false SOULCORE_UnrealBridge__ConnectOnStartup=false SOULCORE_Inference__Enabled=false \
  dotnet run --project SoulCore.Host/SoulCore.Host.csproj
curl -s http://127.0.0.1:7700/health
# → HTTP 200, status=ok, memory.open=true

# Concurrent health + unit soak
./SoulCore/scripts/soak-prop54-concurrent.sh
# → SUMMARY {"pass":true,"healthOk":30,"healthFail":0,...}
```

## Test output (excerpt)

```
Passed!  - Failed: 0, Passed: 32, Total: 32
  SqlitePathGateConcurrencyTests.MemoryAndCharter_ConcurrentOps_OnSamePath_DoNotThrow
  SqlitePathGateConcurrencyTests.MemoryAndCharter_ConcurrentSoak_ChatAndCharterReads_NoSqliteErrors
  Prop54ConcurrentSoakTests.MemoryCharterAndSoulLoop_ConcurrentSoak_NoSqliteConcurrencyErrors
  Prop54ConcurrentSoakTests.SoulLoop_OverlappingTicksDuringDbStorm_SingleFlightSkipsWithoutSqliteErrors
  SoulLoopScaffoldSingleFlightTests.TickAsync_OverlappingCallers_OnlyOneExecutesBody
  SoulLoopScaffoldSingleFlightTests.TickAsync_ParallelInvokes_TickCounterNotLost
```

## Host / health soak log (excerpt)

```
[2026-09-05 18:16:55] === PROP-5.4 concurrent soak start ===
[2026-09-05 18:16:55] Started dotnet test pid=11612
[2026-09-05 18:16:55] PROBE 1 OK status=ok memOpen=True
...
[2026-09-05 18:17:26] PROBE 30 OK status=ok memOpen=True
[2026-09-05 18:17:27] HealthOk=30 HealthFail=0 MaxFailStreak=0
[2026-09-05 18:17:27] SUMMARY {"pass":true,"healthOk":30,"healthFail":0}
```

Full logs: `SoulCore/scripts/logs/prop54-soak-20260905-181655.log`, `SoulCore/scripts/logs/prop54-test-20260905-181655.log`.

## Artifacts added (QA)

| File | Purpose |
| --- | --- |
| `SoulCore/SoulCore.Protocol.Tests/Prop54ConcurrentSoakTests.cs` | Combined memory + charter + SoulLoop soak; tick storm + DB storm |
| `SoulCore/SoulCore.Protocol.Tests/SqlitePathGateConcurrencyTests.cs` | Extended 200-iteration chat/observation/charter recall soak |
| `SoulCore/scripts/soak-prop54-concurrent.sh` | Linux-friendly `/health` probe while unit soak runs |

## Notes

- **SMS path:** No dedicated SMS episodic harness in tree; soak uses `observation` and `chat` source labels (same `WriteEpisodicAsync` + gate path Host uses for companion traffic).
- **Full Protocol.Tests:** `SystemFilesystemToolsTests.ReadFile_SymlinkPointingOut_RejectsWithSuccessFalse` fails on Linux (symlink semantics); pre-existing, out of PROP-5 scope.
- **Host:** Boots successfully on Linux cloud agent VM (not Blocked-partial). Windows PowerShell soak (`soak-soulcore.ps1`) remains the long-duration continuity path for Kurt's machine.

## Recommendation

Accept PROP-5.4 and close PROP-5 cluster QA gate. PM may accept PROP-5.1–5.4 as a bundle for release notes.
