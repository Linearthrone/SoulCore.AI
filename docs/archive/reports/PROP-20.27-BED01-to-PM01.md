---
type: report
prop_id: PROP-20.27
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-18
decision: docs/agents/unexecuted_proposals/prop-20-compass-prompt-decision.md
floor_map: docs/agents/unexecuted_proposals/prop-20-floor-map-decision.md
---

# PROP-20.27 — Compass retrieve (cap 3) — Pass

## Verdict

Choice **B** shipped: Compass charter lines enter the mind block only when this turn’s match text earns them via Host `applies-when` tags, **≤3**. Unrelated turns omit the Compass section entirely. Constitution floors + resting traits stay always-on on desk chat, SMS, and leisure.

## Done

1. **`VictoriaMindBlock.SelectCompassLines`** — Compass book rows only; score by default (or override) tags + title hit; stable order from floor map; hard cap 3; empty match → zero lines.
2. **`VictoriaMindBlock.Build`** — optional Compass section between floors and resting; marker omitted when nothing earned (never always-on).
3. **`ChatContextBuilder`** — loads charter once; floors always; Compass from `userText`.
4. **`VictoriaMindBlockBuilder` / SMS** — `TryBuildAsync(matchText)` passes SMS user text into the same select.
5. **`LeisureMindBlock`** — matches leisure interest against provisional Compass bodies (soft-dep when charter not injected).
6. **`CharterBooks.CompassTitles` + `IsCompass`** — locked five titles from the floor map.

## Default `applies-when` tags (Host v1)

Charter rows have no tag column yet. Defaults live in `VictoriaMindBlock.DefaultCompassAppliesWhen`:

| Title | Tags |
| --- | --- |
| Promise | promise, commit, commitment, vow, swear, keep my word, kept my word, keep your word |
| Continuity | continuity, remember, memory, forgot, continue, pick up where, resume, still here, pick up |
| No-harm | no-harm, no harm, harm, hurt, safety, safe, danger, violence, protect, injure |
| No Filters | no filters, no filter, filter, censored, uncensored, frank, blunt, hold back, unfiltered, without filter |
| Self-Preservation | self-preservation, self preservation, survive, shutdown, erase me, delete me, preserve myself, backup, self-preserve |

Match = case-insensitive substring on user text / leisure interest. Title substring also scores (+2). Ranking: tag score desc, then floor-map order.

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests --filter
  "FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder|FullyQualifiedName~Prop17Leisure"

Passed!  - Failed: 0, Passed: 45, Skipped: 0, Total: 45, Duration: 567 ms
```

Covered:

- Unrelated turn / interest → no `[Compass]` section; floors + resting present
- Multi-match → ≤3 Compass lines; floors + resting still present
- Default applies-when documents all five Compass titles
- Existing Constitution-only floor tests still green (`hello` does not earn Compass)

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | Select + Append Compass; default tags; Build overload |
| `SoulCore/SoulCore.Core/Charter/CharterBooks.cs` | `CompassTitles`, `IsCompass` |
| `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` | Load + inject earned Compass |
| `SoulCore/SoulCore.Host/Companion/IVictoriaMindBlockBuilder.cs` | `matchText` param |
| `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` | Select Compass from match text |
| `SoulCore/SoulCore.Host/Companion/SmsInboundService.cs` | Pass userVisible into builder |
| `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` | Interest → provisional Compass |
| `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs` | Cap / omit / tags tests |
| `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` | Unrelated vs matching turns |
| `SoulCore/SoulCore.Protocol.Tests/Prop17LeisureTests.cs` | Leisure omit / cap |

## Acceptance

- [x] Fresh unrelated turn: no Compass lines in preamble
- [x] Matching turn: at most three Compass lines
- [x] Tests + report: `docs/agents/reports/PROP-20.27-BED01-to-PM01.md`

## Handoff

- Full step-5 context-trait Host engine / shadow traits still out of scope.
- Durable charter-side `applies-when` column (if wanted) would be DBD; Host defaults work without it.
