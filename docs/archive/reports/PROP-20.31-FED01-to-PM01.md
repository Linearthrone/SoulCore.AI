---
type: report
prop_id: PROP-20.31
prop_root: PROP-20-victoria-mind-path
from: FED-01
to: PM-01
priority: P1
status: Pass
created: 2026-11-18
task: docs/agents/tasks/PROP-20.31-PM01-to-FED01.md
depends_on: PROP-20.30
---

# PROP-20.31 — FED-01 → PM-01

**Result: Pass**

Identity tab now paints Host `restingTraits` (weight · band · ledgerReason), turn-scoped `contextTraits` (empty on bare GET shown honestly), and `contextTraitCatalog` (available kinds). No fabricated biography; Host unreachable stays unreachable.

## Acceptance

| Criterion | Result |
| --- | --- |
| Resting trait weight / band / last ledger reason from Host | **Pass** |
| Context traits this turn read-only (empty when none) + catalog | **Pass** |
| No fabricated biography; unreachable stays unreachable | **Pass** |
| Build + smoke evidence | **Pass** (below) |

## Delivered

1. **`IdentityTraitSnapshot` + parse** — `SoulCoreIdentitySettingsClient` maps `restingTraits`, `contextTraits`, `contextTraitCatalog` (weight / band / ledgerReason / kind / appliesWhen).
2. **`IdentityAnchorsFormatter`** — `FormatRestingTraitsBlock` / `FormatContextTraitsBlock`; empty/unreachable → explicit “not fabricated”.
3. **Identity tab UI** — Resting traits + Context traits sections under Charter anchors; refresh paints Host rows into `IdentityRestingTraitsText` / `IdentityContextTraitsText`.
4. **Unit coverage** — parse + format + no-fabrication cases for trait fields.

## Verify

### Build

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release `
  -p:OutputPath=bin\prop2031-out\ -p:IntermediateOutputPath=obj\prop2031-int\ -p:UseAppHost=false --no-incremental
  → Build succeeded. 0 Warning(s). 0 Error(s).
  → House.ChatDesktop -> …\bin\prop2031-out\House.ChatDesktop.dll

dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Debug
  → Build succeeded (default Debug bin; Release default bin locked by running House.ChatDesktop PID 55120)
```

### Tests

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj -c Debug `
  --filter FullyQualifiedName~SoulCoreIdentitySettingsClientTests --verbosity normal

Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.FormatAnchorsBlock_EmptyOrError_DoesNotFabricate
Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.FormatContextTraitsBlock_EmptyTurn_ShowsCatalog_NoFabrication
Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.ParseResponseJson_NumericId_MapsToStringSnapshot
Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.ParseResponseJson_StringId_StillWorks
Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.FormatAnchorsBlock_RendersLiveTitlesAndBodies
Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.FormatRestingTraitsBlock_ShowsWeightBandLedger
Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.ParseResponseJson_RestingAndContextTraits_MapsWeightBandLedger

Test Run Successful.
Total tests: 7
     Passed: 7
```

### Live Host smoke (`GET /settings/identity`)

Earlier in this session Host answered on `127.0.0.1:7710`:

```text
STATUS=200
restingTraits=9
contextTraits=0
contextTraitCatalog=6
sample=Dominant weight=0.7 band=Mid ledger=operator
```

Matches PROP-20.30 contract (nine resting always-on; bare GET `contextTraits=[]`; catalog seeded). Re-probe later in the same session found Host down (`7710`/`7700` unreachable) — UI path then paints Host unreachable + “(No resting/context traits fabricated.)”, not invented rows.

Formatter sample (unit evidence, Host-shaped DTO):

```text
Resting traits (always on) · 1
Dominant · 0.70 Mid · ledger: operator

Context traits (this turn)
(none earned this turn — bare /settings/identity is turn-scoped empty)

Context trait catalog (available kinds)
Curious · 0.52 Mid · topic · ledger: operator-provisional-until-seed
```

## Files

| Path | Change |
| --- | --- |
| `House/House.ChatDesktop/Services/SoulCoreIdentitySettingsClient.cs` | Trait DTO/snapshot + JSON map |
| `House/House.ChatDesktop/Services/IdentityAnchorsFormatter.cs` | Resting / context formatters |
| `House/House.ChatDesktop/MainWindow.axaml` | Resting + Context sections |
| `House/House.ChatDesktop/MainWindow.axaml.cs` | Wire refresh → trait blocks |
| `House/House.ChatDesktop.Tests/SoulCoreIdentitySettingsClientTests.cs` | PROP-20.31 parse/format tests |
| `docs/agents/reports/PROP-20.31-FED01-to-PM01.md` | This report |

## Out of scope (confirmed not done)

- Editing weights from UI
- Bond counters (step 7)

## Handoff

- PM may Accept PROP-20.31 and release QA (PROP-20.32) if queued.
- Operator: restart ChatDesktop to pick up the new Identity panel (running instance held Release `bin` lock during build).
