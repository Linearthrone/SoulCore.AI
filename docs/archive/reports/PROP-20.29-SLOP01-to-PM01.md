---
type: report
prop_id: PROP-20.29
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.29
related_qa: docs/agents/reports/PROP-20.28-QA01-to-PM01.md
related_dev: docs/agents/reports/PROP-20.27-BED01-to-PM01.md
status: clean
created: 2026-11-18
---

# SLOP-01 Report — PROP-20.29

## Scope

- Related QA: PROP-20.28 Pass (Compass retrieve B / cap 3)
- Related DEV delta: PROP-20.27 Choice B
- Files / packages scanned:
  - `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` (`SelectCompassLines`, `DefaultCompassAppliesWhen`, `AppendCompass`, `Build`)
  - `SoulCore/SoulCore.Core/Charter/CharterBooks.cs` (`CompassTitles`, `IsCompass`)
  - `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` (`LoadCharterMindPartsSafeAsync`)
  - `SoulCore/SoulCore.Host/Companion/IVictoriaMindBlockBuilder.cs`
  - `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs`
  - `SoulCore/SoulCore.Host/Companion/SmsInboundService.cs` (`TryBuildAsync(userVisible)`)
  - `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` (provisional Compass + shared `SelectCompassLines`)
  - Tests (read for surface confirmation): `VictoriaMindBlockTests`, `ChatContextBuilderTests`, `Prop17LeisureTests` Compass cases
- Method: read + ripgrep for `SelectCompass` / `CompassMarker` / `DefaultCompassAppliesWhen` / `IsCompass` / consumer call sites (no code edits)

## Summary

- Findings count: **0**
- Highest severity: n/a
- Verdict: **clean**

## Findings

None.

## Clean areas

- **Single select path:** Desk chat, SMS builder, and leisure all call `VictoriaMindBlock.SelectCompassLines` — no second scorer, no `getCompass` / `fetchCompass` / `loadCompass` alias sprawl.
- **Single applies-when source:** `DefaultCompassAppliesWhen` is the only Host v1 tag table; override param exists but unused by consumers (fine).
- **Cap ownership:** Cap-3 enforced in select; `AppendCompass` re-checks only because public `Build` may receive an arbitrary list — justified, not dead wrapper.
- **Book membership:** `CharterBooks.IsCompass` mirrors `IsConstitutionFloor` structure by design (different books); not same-purpose aliases.
- **Leisure provisional bodies:** Soft-dep snapshot keyed by `CompassTitles` (same pattern as provisional Constitution floors); documented OK-to-drift; still routes through shared select + `Build`.
- **SMS wiring:** `SmsInboundService` passes `userVisible` into `TryBuildAsync(matchText)` — one consumer path, no parallel SMS-side matching.
- **Truncate reuse:** Compass lines use `TruncateFloorLine` — no copy-pasted truncator.

## Notes for PM

- Safe to archive? **yes** (QA Pass + SLOP clean on PROP-20.27 Compass retrieve delta).
- Optional non-blocking polish (do **not** block done): collapse `IsConstitutionFloor` / `IsCompass` to a private `MatchesBook` helper, or share leisure provisional-row builders — style only, no divergent-behavior risk today.
- Durable charter-side `applies-when` column remains out of scope (BED handoff → DBD if wanted later).
