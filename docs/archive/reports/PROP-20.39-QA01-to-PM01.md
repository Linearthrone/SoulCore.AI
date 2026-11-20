---
type: report
prop_id: PROP-20.39
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
title: QA Step 7 growth / soak / band-cross / Identity
depends_on: PROP-20.37
upstream: docs/agents/reports/PROP-20.37-BED01-to-PM01.md
---

# PROP-20.39 — QA-01 → PM-01

**Result: Pass**

Growth engine + Identity fields verified with real `dotnet test` output and live `GET /settings/identity` against the House Victoria Host on **:7710** (v0.1.54). Soak is display-only (`mayChangePrompt=false`); no prompt growth pre-soak. Band-cross confirm/dismiss HTTP routes respond correctly.

## Verdict matrix

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Bond counters from Host facts | **Pass** | Unit: `Store_Migration014_SeedsTraits_AndBondCounters` → `days=3 minutes=5`; Live Identity `bondCounters` present |
| 2 | 14d soak display-only; no prompt/routing change pre-soak | **Pass** | Unit: `Service_DisplayOnly_*` + `SoakClock_DisplayOnly_*`; Live `phase=display_only` `mayChangePrompt=false` |
| 3 | &lt;3 contact days keeps soak closed | **Pass** | Unit: `SoakClock_GateClosed_*` + `Service_ContactGate_*` → `contactDays=1 phase=gate_closed` |
| 4 | Inside-band auto-apply + ledger | **Pass** | Unit: `DeltaEngine_InsideBand_AutoApplies` + open service test `inside=AutoApply` |
| 5 | Band-cross → pending until confirm | **Pass** | Unit: `DeltaEngine_BandCross_StaysPending` + open service `cross=Pending confirmedWeight=0.85` |
| 6 | Confirm / dismiss endpoints | **Pass** | Confirm covered in unit service test; live `POST …/confirm|dismiss` on missing id → `404 {"ok":false,"error":"pending_not_found"}` |
| 7 | Identity JSON fields (`bondCounters`/`soak`/`growthTraits`/`pendingBandCross`/`growthLedger`) | **Pass** | Live `:7710` Identity keys include all five + seed Mid 0.52 traits |
| 8 | No `[Growth traits]` pre-soak | **Pass** | Unit: `MindBlock_OmitsGrowth_WhenNull_*` + `GetPromptGrowthTraitsAsync` null while display-only |

## Commands run

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~Prop2037GrowthBondTests" --nologo --verbosity minimal

Invoke-WebRequest http://127.0.0.1:7710/health -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:7710/settings/identity -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:7710/settings/identity/growth/pending/999999/confirm -Method POST -ContentType application/json -Body '{}'
Invoke-WebRequest http://127.0.0.1:7710/settings/identity/growth/pending/999999/dismiss -Method POST -ContentType application/json -Body '{}'
```

## Test evidence (real output)

### Prop2037GrowthBondTests — 14/14 Pass

```text
[PROP-20.37] gate-closed contactDays=1 phase=gate_closed
[PROP-20.37] open inside=AutoApply cross=Pending confirmedWeight=0.85 promptGrowth=3
[PROP-20.37] bondCounters days=3 minutes=5 tasks=0
[PROP-20.37] display-only phase=display_only mayChange=False decision=Rejected/pre_soak_display_only

Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14
Test Run Successful.
Total tests: 14
     Passed: 14
```

### Live Host Identity (:7710 / v0.1.54)

```text
health version=0.1.54 memory=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db port_claim=7710
identity keys=displayName,contactId,charter,identityAnchors,anchors,restingTraits,contextTraits,contextTraitCatalog,bondCounters,soak,growthTraits,pendingBandCross,growthLedger,note

soak={"phase":"display_only","startedAt":"2026-11-19T02:11:04.143Z","daysElapsed":0,"daysRemaining":14,"contactDaysInWindow":0,"contactGateMet":false,"minContactDays":3,"displayOnlyDays":14,"isFirstWeekAfterOpen":false,"mayChangePrompt":false}
bond={"contactTimeMinutes":0,"daysWithContact":0,"entrustedTasks":0,"operatorCorrections":0,"herInitiatedReaches":0,"journalOpens":3}
growthCount=3
  responsiveness w=0.52 band=Mid
  initiative w=0.52 band=Mid
  openness w=0.52 band=Mid

POST confirm -> 404 {"ok":false,"id":999999,"error":"pending_not_found"}
POST dismiss -> 404 {"ok":false,"id":999999,"error":"pending_not_found"}
```

### DB (read-only) — migration 014 present

```text
schema_migrations: 014 | 2026-11-19T02:10:47.089Z
growth_soak_state: started_at=2026-11-19T02:11:04.143Z
growth_trait_weights: responsiveness/initiative/openness @ 0.52 Mid
growth_pending_proposals: (empty)
growth_delta_ledger: (empty)
```

## Dual-Host note (non-blocking for this PROP)

| Port | Version | Tree | Identity growth fields |
| --- | --- | --- | --- |
| **7710** | **0.1.54** | `SoulCore.AI` Release (ALLSTART default) | **Present — used for Pass** |
| 7700 | 0.1.52 | Foreign `C:\Users\kurtw\Soul_Core\...` Debug | Absent (pre-PROP-20.37) |

ChatDesktop / ALLSTART default to **:7710**. Stale foreign Host on :7700 is recorded as `docs/agents/issues/ISSUE-20261118-001-stale-foreign-host-on-7700.md` for OPS cleanup (PROP-20.37b still Pending).

## Observations (not Fail)

- No dedicated unit test calls `DismissPendingAsync` with a real pending row (confirm path is covered). HTTP dismiss route is live and returns the expected JSON NotFound contract.
- First-week small-nudge + non-nudgeable trait reject covered in unit suite only (no live pending to confirm/dismiss without mutating soak).

## Ready for

- **SLOP-01** post-QA slop audit (code-change Pass)
- OPS-01 finish **PROP-20.37b** (kill foreign :7700 / ensure single House Victoria Host)
- FED-01 **PROP-20.38** may consume Identity growth fields from `:7710`

## QA red lines

No product code modified. No OPS deploy performed. Evidence only.
