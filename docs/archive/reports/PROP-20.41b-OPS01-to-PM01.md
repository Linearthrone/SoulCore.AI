---
type: report
prop_id: PROP-20.41b
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-18
seat: OPS-HOME
upstream_task: docs/agents/tasks/PROP-20.41b-PM01-to-OPS01.md
depends_on: PROP-20.41
---

# PROP-20.41b — OPS report (Release Host hygiene)

## Verdict: **Pass**

Serialized Release ALLSTART with `-RestartHost -ForceRebuild` refreshed Host on `:7710` so live process loads PROP-20.41 (`ResolveSoakAsync` / seed SoT). `GET /health` is ok; `GET /settings/identity` returns `bondCounters` and `soak`.

## Serialize

- Checked for in-flight `ALLSTART.ps1` — none.
- Acquired `docs/agents/.ops-allstart.lock` (holder OPS-01 / PROP-20.41b).
- Pre-check: `GrowthService.cs` LastWriteTime `21:19:55` was newer than Release `SoulCore.Host.dll` `21:10:45` → ForceRebuild required.
- Released lock after probes / this report.

## Commands / evidence

```text
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild -SkipVoice -SkipPlaywrightInstall
# tee: docs/agents/reports/_allstart-prop-20.41b-release.log
```

Host start excerpt (from ALLSTART log):

```text
RestartHost: replacing local Victoria on :7710
RestartHost: clearing every SoulCore.Host (+ :7710 listener)
Stopping PID 31064 (dotnet) - SoulCore.Host (-RestartHost)
Building SoulCore (Release)...
Build succeeded.  4 Warning(s)  0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 4896
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
  model:  deepseek-v4.1-flash:cloud
```

### DLL freshness (PROP-20.41 included)

```text
GrowthService.cs=2026-11-18T21:19:55-05:00
HostDLL=2026-11-18T21:22:45-05:00
CoreDLL=2026-11-18T21:22:44-05:00
dll_includes_PROP2041_source=True
ResolveSoakAsync_refs_in_GrowthService=5
HostPID=4896 StartTime=2026-11-18 21:22:46
```

### Health

```text
GET http://127.0.0.1:7710/health -> status=ok service=SoulCore.Host version=0.1.54 port=7710 memory.open=True
memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
inference.provider=ollama-cloud model=deepseek-v4.1-flash:cloud
```

### Identity probe (`GET http://127.0.0.1:7710/settings/identity`)

```text
HAS_bondCounters=True
HAS_soak=True
BOND={"contactTimeMinutes":0,"daysWithContact":0,"entrustedTasks":0,"operatorCorrections":0,"herInitiatedReaches":0,"journalOpens":21}
SOAK={"phase":"display_only","startedAt":"2026-11-19T02:11:04.143Z","daysElapsed":0,"daysRemaining":14,"contactDaysInWindow":0,"contactGateMet":false,"minContactDays":3,"displayOnlyDays":14,"isFirstWeekAfterOpen":false,"mayChangePrompt":false}
```

## Checklist

| Check | Result |
| --- | --- |
| Serialize (no overlapping ALLSTART; lock held) | Pass |
| `ALLSTART -Configuration Release -RestartHost -ForceRebuild` Host healthy | Pass |
| Live Host DLL ≥ PROP-20.41 `GrowthService.cs` | Pass |
| `GET /health` status=ok on `:7710` | Pass |
| Identity `bondCounters` present | Pass |
| Identity `soak` present | Pass |

## Notes

- ChatDesktop Release started (PID **57492**) against `:7710`; ALLSTART remains blocked on foreground `dotnet run` in `start-desktopgui.ps1` (same pattern as PROP-20.37b). Host acceptance for **41b** is green regardless.
- `-SkipVoice` / `-SkipPlaywrightInstall` used to keep the Host refresh path tight; Tailscale serve re-enabled for `:7710` / HTTPS `:8543`.
- No `deploy_history.json` bump — Host hygiene refresh, not a versioned product cut.
