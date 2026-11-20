---
type: report
prop_id: PROP-20.37
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-18
title: Step 7 — bond counters + soak gate + growth delta rules
unblocks: PROP-20.38
---

# PROP-20.37 — BED-01 → PM-01

**Result: Pass**

Bond counters, 14-day soak (+3 contact-day gate), inside-band auto-apply with ledger, band-cross pending until operator confirm, and post-soak nudges limited to responsiveness / initiative / openness. Identity API fields ready for FED-20.38.

## Law checklist

| Rule | Status |
| --- | --- |
| Bond counters from Host facts (contact time, days, tasks, corrections, reaches, journals) | Pass |
| 14-day display-only; no prompt/routing change pre-soak | Pass |
| Soak stays closed if &lt;3 contact days in window | Pass |
| Inside-band auto-apply + ledger | Pass |
| Band-cross → pending until operator confirm | Pass |
| Post-soak nudges only responsiveness / initiative / openness | Pass |
| First week after open: small inside-band only | Pass |
| Identity API exposes counters + soak + growth + pending | Pass |
| Persist pending + ledger (migration 014) | Pass |

## What shipped

1. **Core growth engine** (`SoulCore.Core/Growth/`): bands (Low/Mid/High), soak clock, delta engine, three seed growth traits.
2. **Migration 014** (`growth_soak_state`, `growth_trait_weights`, `growth_delta_ledger`, `growth_pending_proposals`).
3. **`SqliteGrowthRepository` / `IGrowthStore`**: soak start, weights, ledger, pending, bond counter SQL from chat / episodic / tasks / journals.
4. **`GrowthService`**: Identity snapshot, prompt gate, apply/confirm/dismiss, daily counter nudge evaluate (post-soak only).
5. **`GET /settings/identity`**: new `bondCounters`, `soak`, `growthTraits`, `pendingBandCross`, `growthLedger`.
6. **Confirm/dismiss**: `POST /settings/identity/growth/pending/{id}/confirm|dismiss`.
7. **Mind block**: `[Growth traits]` section only when soak `mayChangePrompt` (desk + SMS builders).

## Identity API (FED-20.38)

Endpoint: `GET /settings/identity`

New fields (alongside existing `restingTraits`):

```json
{
  "bondCounters": {
    "contactTimeMinutes": 0.0,
    "daysWithContact": 0,
    "entrustedTasks": 0,
    "operatorCorrections": 0,
    "herInitiatedReaches": 0,
    "journalOpens": 0
  },
  "soak": {
    "phase": "display_only",
    "startedAt": "2026-11-18T…Z",
    "daysElapsed": 0,
    "daysRemaining": 14,
    "contactDaysInWindow": 0,
    "contactGateMet": false,
    "minContactDays": 3,
    "displayOnlyDays": 14,
    "isFirstWeekAfterOpen": false,
    "mayChangePrompt": false
  },
  "growthTraits": [
    {
      "id": "responsiveness",
      "name": "Responsiveness",
      "group": "growth",
      "weight": 0.52,
      "band": "Mid",
      "promptLine": "…",
      "ledgerReason": "operator-provisional-until-seed"
    }
  ],
  "pendingBandCross": [],
  "growthLedger": []
}
```

Operator actions:

- `POST /settings/identity/growth/pending/{id}/confirm`
- `POST /settings/identity/growth/pending/{id}/dismiss`

## Migration note (DBD)

BED applied **migration 014** inline (ticket allowed). Tables seed the three growth traits at Mid 0.52. Soak clock starts on first Identity GET (`EnsureSoakStartedAsync`).

## Files

| Path | Role |
| --- | --- |
| `SoulCore.Core/Growth/*.cs` | Bands, soak, delta rules, trait seeds |
| `SoulCore.Core/VictoriaMindBlock.cs` | Optional `[Growth traits]` section |
| `SoulCore.Memory/Migrations/014_growth_bond.sql` | Schema |
| `SoulCore.Memory/Repositories/SqliteGrowthRepository.cs` | Persist + bond counters |
| `SoulCore.Host/Growth/GrowthService.cs` | Host orchestration |
| `SoulCore.Host/Hosting/WebApplicationExtensions.cs` | Identity + confirm APIs |
| `SoulCore.Host/Ws/ChatContextBuilder.cs` | Prompt gate |
| `SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` | SMS prompt gate |
| `SoulCore.Protocol.Tests/Prop2037GrowthBondTests.cs` | 14 tests |

## Test evidence (real output)

```text
dotnet test SoulCore/SoulCore.Protocol.Tests --filter FullyQualifiedName~Prop2037GrowthBondTests

[PROP-20.37] gate-closed contactDays=1 phase=gate_closed
[PROP-20.37] open inside=AutoApply cross=Pending confirmedWeight=0.85 promptGrowth=3
[PROP-20.37] bondCounters days=3 minutes=5 tasks=0
[PROP-20.37] display-only phase=display_only mayChange=False decision=Rejected/pre_soak_display_only

Total tests: 14
     Passed: 14
```

Covered: display-only reject; no prompt growth pre-soak; contact gate; inside-band ledger apply; band-cross pending + confirm; mind-block omit/include.

## Out of scope (held)

- Full Identity UI → **FED-20.38** (unblocked)
- Step 8 second wave
- Changing Constitution floors from bond

## Next

PM: Accept → release **PROP-20.38** to FED-01.
