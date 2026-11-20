---
type: report
prop_id: PROP-20.38
prop_root: PROP-20-victoria-mind-path
from: FED-01
to: PM-01
priority: P1
status: Pass
created: 2026-11-18
title: Step 7 — Identity bond counters + pending band-cross confirm
depends_on: PROP-20.37
---

# PROP-20.38 — FED-01 → PM-01

**Result: Pass**

Identity tab now paints Host `bondCounters`, `soak`, `growthTraits`, `pendingBandCross`, and `growthLedger`. Pending rows wire Confirm / Dismiss to `POST /settings/identity/growth/pending/{id}/confirm|dismiss`. No fabricated counters when Host is down or fields absent.

## Law checklist

| Rule | Status |
| --- | --- |
| Bond counters from Host (read-only) | Pass |
| Soak state display-only (phase / days remaining / contact gate) | Pass |
| Pending band-cross confirm + dismiss → Host APIs | Pass |
| No fabricated counters; unreachable stays unreachable | Pass |
| Build + smoke evidence | Pass (see notes) |

## What shipped

1. **`SoulCoreIdentitySettingsClient`** — parse `bondCounters`, `soak`, `growthTraits`, `pendingBandCross`, `growthLedger`; `ConfirmPendingAsync` / `DismissPendingAsync`.
2. **`IdentityAnchorsFormatter`** — bond / soak / growth / pending status / ledger blocks; Host-down copy refuses invented zeros.
3. **Identity tab (Avalonia)** — Bond counters, Soak, Growth traits, Pending band-cross list (Confirm/Dismiss), Growth ledger.
4. **Tests** — three new cases for PROP-20.37 JSON shape + no-fabrication + Host facts formatting.

## Files

| Path | Role |
| --- | --- |
| `House/House.ChatDesktop/Services/SoulCoreIdentitySettingsClient.cs` | DTOs + GET/POST growth |
| `House/House.ChatDesktop/Services/IdentityAnchorsFormatter.cs` | Display formatters |
| `House/House.ChatDesktop/MainWindow.axaml` | Identity growth sections |
| `House/House.ChatDesktop/MainWindow.axaml.cs` | Refresh + confirm/dismiss handlers |
| `House/House.ChatDesktop.Tests/SoulCoreIdentitySettingsClientTests.cs` | Parse/format tests |

## Test evidence (real output)

```text
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj -c Release -p:UseAppHost=false -o %TEMP%\House.ChatDesktop.prop2038
Build succeeded. 0 Warning(s) 0 Error(s)

dotnet build House/House.ChatDesktop.Tests/... -c Release -p:UseAppHost=false -o %TEMP%\House.ChatDesktop.Tests.prop2038
dotnet test %TEMP%\House.ChatDesktop.Tests.prop2038\House.ChatDesktop.Tests.dll --filter FullyQualifiedName~SoulCoreIdentitySettingsClientTests

  Passed ParseResponseJson_BondSoakGrowthPending_MapsHostFields
  Passed FormatBondSoakPending_HostDown_DoesNotFabricateCounters
  Passed FormatBondSoakGrowth_ShowsHostFacts
  (+ 7 prior identity tests)

Test Run Successful.
Total tests: 10
     Passed: 10
```

Default `bin\Release` apphost copy failed while **House.ChatDesktop (PID 54596)** held the exe lock — same sources built cleanly via `-p:UseAppHost=false` / alternate `-o`.

## Live Host smoke (honest)

Running Host **0.1.52** on `:7700` still serves **pre–PROP-20.37** Identity JSON (no `bondCounters` / `soak` / `growthTraits` / `pendingBandCross`). Confirm/dismiss routes return bare **HTTP 404**.

```text
GET /health → 200 version=0.1.52
GET /settings/identity → 200, NO_bondCounters NO_soak NO_growthTraits NO_pendingBandCross
POST .../growth/pending/999/confirm → 404
POST .../growth/pending/999/dismiss → 404
```

Fresh Host build (`%TEMP%\SoulCore.Host.prop2038`) **does** contain `bondCounters` / `pendingBandCross` symbols. Desk UI will show Host facts after OPS restarts Host with the 20.37+ binary; until then the panel correctly shows empty / not-invented (not fake zeros).

## Out of scope (held)

- Hand-editing bond facts
- Step 8
- Self-deploy / Host restart (OPS)

## Next

PM: Accept → OPS Host restart with PROP-20.37+ for live counters; optional QA glance at Identity tab Confirm/Dismiss once pending rows exist.
