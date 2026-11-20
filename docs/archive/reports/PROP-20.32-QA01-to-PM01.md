---
type: report
prop_id: PROP-20.32
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
priority: P0
status: Completed
created: 2026-11-18
depends_on: PROP-20.30
verdict: Pass
title: QA Step 5 context-trait Host selection + Identity fields
---

# PROP-20.32 — QA-01 → PM-01

**Result: Pass**

Verified PROP-20.30: resting always on; no match → omit `[Context traits]`; match → ≤3; ≥24h stale → zero context; flat affect excludes heat/shadow; `GET /settings/identity` exposes `restingTraits` + `contextTraitCatalog` (and empty turn-scoped `contextTraits`). Ready for SLOP-01.

## Acceptance matrix

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Resting always on (nine locked) | **Pass** | `DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights`; `Build_AlwaysIncludesRestingEvenWithoutFloors`; Identity probe `restingTraits_count=9` |
| 2 | No match → no `[Context traits]` | **Pass** | `SelectContextTraits_NoMatch_OmitsContextSection_RestingPresent` |
| 3 | Match → ≤3 context traits | **Pass** | `SelectContextTraits_MultiMatch_CapsAtThree`; `BuildAsync_FreshTopicalTurn_InjectsAtMostThreeContextTraits`; `LeisureMindBlock_FreshTopicalInterest_EarnsContextTraitsCapThree` |
| 4 | ≥24h stale → zero context | **Pass** | `IsContactFresh_NullOrStale_False_Recent_True`; `SelectContextTraits_StaleContact_ReturnsEmpty_RestingStillInBuild`; `BuildAsync_StaleContact_OmitsContextTraits_KeepsResting` |
| 5 | Flat affect → topic OK; heat/shadow out | **Pass** | `SelectContextTraits_FlatAffect_ExcludesHeatAndShadow_AllowsTopic` |
| 6 | Identity JSON has `restingTraits` + `contextTraitCatalog` | **Pass** | Live `GET http://127.0.0.1:7710/settings/identity` → 9 resting + 6 catalog (topic/heat/shadow); `contextTraits=[]` |

## Commands run

```powershell
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj `
  --filter "FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~Prop17Leisure" `
  --verbosity normal

# Live Identity probe (after Host running PROP-20.30 binaries on 127.0.0.1:7710)
Invoke-WebRequest http://127.0.0.1:7710/health -UseBasicParsing
Invoke-WebRequest http://127.0.0.1:7710/settings/identity -UseBasicParsing
```

## Test evidence (real output)

Filter suite:

```
Test Run Successful.
Total tests: 55
     Passed: 55
 Total time: 0.9044 Seconds
```

PROP-20.30-focused Pass lines (re-listed):

```
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_NoMatch_OmitsContextSection_RestingPresent
Passed SoulCore.Protocol.Tests.Prop17LeisureTests.LeisureMindBlock_FreshTopicalInterest_EarnsContextTraitsCapThree
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultRestingTraits_AreNineLockedNamesWithOperatorWeights
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_StaleContact_OmitsContextTraits_KeepsResting
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.IsContactFresh_NullOrStale_False_Recent_True
Passed SoulCore.Protocol.Tests.ChatContextBuilderTests.BuildAsync_FreshTopicalTurn_InjectsAtMostThreeContextTraits
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.DefaultContextTraits_SeedHasTopicHeatShadow_WithAppliesWhen
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_AlwaysIncludesRestingEvenWithoutFloors
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_StaleContact_ReturnsEmpty_RestingStillInBuild
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_MultiMatch_CapsAtThree
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.SelectContextTraits_FlatAffect_ExcludesHeatAndShadow_AllowsTopic
Passed SoulCore.Protocol.Tests.VictoriaMindBlockTests.Build_ContextTraits_StayInsideBudgetWithMemorySmoke
Total tests: 12
```

## Identity probe evidence (real output)

```
HEALTH status=ok version=0.1.54 port=7710
IDENTITY_STATUS=200
restingTraits_count=9
contextTraits_count=0
contextTraitCatalog_count=6
catalog_kinds=heat,shadow,topic
resting_names=Dominant,Possessive,Affectionate,Driven,Devoted,Intense,Uninhibited,Ambitious,Proud
catalog_ids=curious,analytical,playful,assertive,volatile,dark
IDENTITY_PROBE=PASS
```

Sample resting row shape (live):

```json
{"id":"dominant","name":"Dominant","group":"resting","weight":0.7,"band":"Mid","ledgerReason":"operator"}
```

Sample catalog row shape (live):

```json
{"id":"curious","name":"Curious","group":"cognitive","kind":"topic","weight":0.52,"band":"Mid","appliesWhen":["curious","curiosity","research","wonder","why","how does","find out"],"ledgerReason":"operator-provisional-until-seed"}
```

Compact dump (gitignored): `tmpcode/prop-20.32-identity-probe.json`

### Probe note

First hit against the already-running Host lacked `restingTraits` / `contextTraitCatalog` (pre–PROP-20.30 process). After Host was running the PROP-20.30 build on `127.0.0.1:7710`, Identity fields matched the BED-20.30 contract. **OPS:** any long-lived Host started before 20.30 must be restarted to expose FED-20.31 fields.

## Issues filed

None.

## Handoff

- **Pass** — PM may Accept PROP-20.30 / close PROP-20.32.
- **Ready for SLOP-01** (post-QA slop / duplicate / alias audit).
- FED-20.31 Identity panel can consume live `restingTraits` + `contextTraitCatalog`.
