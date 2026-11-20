---
type: report
prop_id: PROP-20.37b
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-18
seat: OPS-HOME
related: PROP-20.38b
---

# PROP-20.37b — OPS report (Release Host + migration 014)

## Verdict: **Pass**

Release ALLSTART with `-RestartHost -ForceRebuild` completed Host rebuild/start on `:7710`. Migration **014** (`growth_bond`) applied; `GET /settings/identity` returns `bondCounters` and `soak`.

## Commands / evidence

```text
.\ALLSTART.ps1 -Configuration Release -RestartHost -ForceRebuild
# tee: docs/agents/reports/_allstart-prop-20.37b-release.log
```

Host start excerpt (from ALLSTART log):

```text
Building SoulCore (Release)...
Build succeeded.  1 Warning(s)  0 Error(s)
Starting SoulCore.Host on http://127.0.0.1:7710 ...
PID: 31064
Local Victoria Host healthy.
  memory: C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

### Migration 014

```text
python sqlite3 probe on soulcore_memory.db:
('014', 'growth_bond', '2026-11-19T02:10:47.089Z')
```

Related tables present: `growth_bond`-era `growth_soak_state`, `growth_trait_weights`, `growth_delta_ledger`, `growth_pending_proposals`.

### Identity probe (`GET http://127.0.0.1:7710/settings/identity`)

```text
HAS_bondCounters=True
HAS_soak=True
BOND={"contactTimeMinutes":0,"daysWithContact":0,"entrustedTasks":0,"operatorCorrections":0,"herInitiatedReaches":0,"journalOpens":9}
SOAK={"phase":"display_only","startedAt":"2026-11-19T02:11:04.143Z","daysElapsed":0,"daysRemaining":14,"contactDaysInWindow":0,"contactGateMet":false,"minContactDays":3,"displayOnlyDays":14,"isFirstWeekAfterOpen":false,"mayChangePrompt":false}
NOTE contains PROP-20.37 guidance: True
```

### Health

```text
GET http://127.0.0.1:7710/health -> status=ok version=0.1.54 port=7710
```

## Notes

- ChatDesktop start inside the same ALLSTART blocked while a prior Presence held the Release apphost (`dotnet run` wait). Host/migration acceptance for **37b** was already green before Presence unlock.
- Presence Release rebuild + start against `:7710` completed under **PROP-20.38b** (serialized on the same Release stack). See `PROP-20.38b-OPS01-to-PM01.md`.
- `.ops-allstart.lock` held by this prop released after report.
