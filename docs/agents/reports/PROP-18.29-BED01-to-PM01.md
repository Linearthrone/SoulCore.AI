---
type: report
from: BED-01
to: PM-01
task_id: PROP-18.29
prop_id: PROP-18.29
depends_on: PROP-18.25
status: Pass
created: 2026-11-19
---

# PROP-18.29 — BED-01 → PM-01

## Verdict

**Pass** — F1 (ActivityLogDto `desc` only) and F2 (Composer paragraph-floor reuse) done; targeted tests green.

## Changes

### F1 — Wire DTO: `desc` only

- **Host** `ActivityLogDto` no longer emits `sessionDesc`. Canonical wire field is `desc` (maps store `SessionDesc`).
- **FED** `SoulCoreActivitiesClient.ActivityDto` dropped `SessionDesc` / `sessionDesc` hedge; maps `Desc` only.
- Desk fixture updated to match wire shape.

### F2 — Writer reuses Composer paragraph count

- Exposed `ActivityResidueComposer.CountParagraphs(string)` (same split rules as private `SplitParagraphs`).
- `ActivityResidueWriter.TryWriteAsync` depth-floor gate calls `CountParagraphs` instead of a twin `"\r\n\r\n"` / `"\n\n"` re-split.

## Files

| File | Change |
| --- | --- |
| `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` | Drop `sessionDesc` from `ActivityLogDto` |
| `SoulCore/SoulCore.Inference/Presence/ActivityResidueComposer.cs` | Add public `CountParagraphs` |
| `SoulCore/SoulCore.Inference/Presence/ActivityResidueWriter.cs` | Use `CountParagraphs` for ¶ floor |
| `House/House.ChatDesktop/Services/SoulCoreActivitiesClient.cs` | `desc` only |
| `House/House.ChatDesktop.Tests/SoulCoreActivitiesClientTests.cs` | Fixture without `sessionDesc` |

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests --filter "FullyQualifiedName~Prop187"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6

dotnet test House/House.ChatDesktop.Tests --filter "FullyQualifiedName~SoulCoreActivitiesClient"
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

## Notes for PM

- Store column remains `session_desc` / `SessionDesc` internally; wire name is `desc` only.
- No further FED task needed for the alias — client updated in this ticket.
