---
type: report
prop_id: PROP-20.34
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
priority: P2
status: Pass
created: 2026-11-18
title: SLOP cleanup — emotion double-read + applies-when scorer merge
depends_on: PROP-20.33
---

# PROP-20.34 — BED-01 → PM-01

**Result: Pass**

Hygiene from PROP-20.33 F1/F2: desk chat emotion is read once per turn; Compass and context-trait applies-when scoring share one private helper. Selection semantics unchanged; targeted tests green.

## Checklist

| Item | Status |
| --- | --- |
| F1 — stop second `_emotion.GetAsync` for flat-affect label | Pass |
| F2 — merge Compass / context applies-when scorers | Pass |
| Selection semantics preserved | Pass |
| Tests green | Pass |

## What changed

### F1 — `ChatContextBuilder` emotion single-read

- Replaced `LoadEmotionPreambleSafeAsync` + `LoadEmotionLabelSafeAsync` with `LoadEmotionPreambleAndLabelSafeAsync`.
- One `_emotion.GetAsync` → `ReadFields` / `DescribeLabel` + `BuildPreamble`; label reused for `SelectContextTraits`.
- On failure: empty preamble (chat continues); `null` label (flat-affect filter skipped) — same soft-dep contracts as before.

### F2 — shared applies-when scorer

- Removed `ScoreContextAppliesWhen`.
- `ScoreAppliesWhen(matchText, nameOrTitle, tags)` now serves both Compass (`title` + tag list from dictionary) and context traits (`row.Name` + `row.AppliesWhen`).
- Rule unchanged: name/title hit +2; each matching tag +1.

## Files

| Path | Change |
| --- | --- |
| `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` | F1 single emotion snapshot |
| `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` | F2 scorer merge |

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~EmotionInfluencePromptTests"

Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35

dotnet test ... --filter "FullyQualifiedName~Prop17LeisureTests|FullyQualifiedName~VictoriaMindBlock|FullyQualifiedName~ChatContextBuilder"
  --no-build

Passed!  - Failed: 0, Passed: 55, Skipped: 0, Total: 55
```

## Notes for PM

- Safe to archive PROP-20.34 with this Pass.
- PROP-20.33 SLOP findings F1/F2 closed; no further BED action required for that surface.
