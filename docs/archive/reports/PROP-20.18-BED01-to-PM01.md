---
type: report
prop_id: PROP-20.18
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
depends_on: PROP-20.17
slop: docs/agents/reports/PROP-20.17-SLOP01-to-PM01.md
---

# PROP-20.18 — CharterFloorMap remove + leisure title dedupe — Pass

## Verdict

**Pass.** F1 deleted dead `CharterFloorMap`; F2 leisure provisional titles now come from `CharterBooks.ConstitutionTitles` (bodies stay local snapshot). F3 skipped (deferred).

## Done

1. **F1** Deleted `SoulCore/SoulCore.Core/Charter/CharterFloorMap.cs`. Repo `.cs` refs: none remaining. Callers stay on `CharterBooks`.
2. **F2** `LeisureMindBlock.ProvisionalFloors` built from `CharterBooks.ConstitutionTitles` + local `ProvisionalFloorBodies` dictionary (soft-dep snapshot OK).
3. **F3** Not extracted — per PM disposition / task skip.

## Evidence

```
dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~CharterServiceTests|FullyQualifiedName~CharterFourBookMigrationTests|FullyQualifiedName~Prop17LeisureTests"
  -o .tmp\prop2018-test

Passed!  - Failed:     0, Passed:    49, Skipped:     0, Total:    49, Duration: 1 s
  - SoulCore.Protocol.Tests.dll (net8.0)
```

Note: default Host `bin\Debug` was locked by a running `.NET Host` process; tests built/ran via alternate `-o .tmp\prop2018-test` (same sources).

Covered:

- VictoriaMindBlock Constitution floor filter
- CharterService / four-book migration soft-compat
- Prop17Leisure / LeisureMindBlock floors + resting + model gate

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/Charter/CharterFloorMap.cs` | **Deleted** |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | Titles ← `CharterBooks.ConstitutionTitles`; bodies local |

## Acceptance

- [x] F1 `CharterFloorMap` removed; no project call sites
- [x] F2 leisure provisional titles deduped from `ConstitutionTitles`
- [x] F3 skipped
- [x] Tests pasted above
- [x] Report: `docs/agents/reports/PROP-20.18-BED01-to-PM01.md`

## Handoff

Hygiene-only; no OPS/QA re-run required unless PM wants leisure provisional floor smoke after accept.
