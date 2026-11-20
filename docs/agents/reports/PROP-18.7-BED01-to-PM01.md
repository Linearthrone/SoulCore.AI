---
type: report
prop_id: PROP-18.7
prop_root: PROP-18-chronology-and-felt-persistence
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
decision: docs/agents/unexecuted_proposals/prop-18-q-decisions.md
depends_on: PROP-18.6
---

# PROP-18.7 — Residue writers (log + journal depth) — Pass

## Verdict: **Pass**

Leisure / research / parked-work sessions now leave inspectable Q1 residue: a **non-generic** activities-log Desc plus a topic-scoped first-person **thoughts** journal append (≈1–2¶ floor, no upper cap). List/read APIs are ready for FED-18.3.

## Done

1. **`ActivityResidueComposer`** — builds session Desc from findings (rejects recycled templates like “Read paper on X”); builds first-person topic journal with ≥2 paragraphs when there is substance; no character cap.
2. **`IActivityResidueWriter` / `ActivityResidueWriter`** — appends `thoughts` journal (tags: topic + activity kind + subject) then activities-log row with journal soft-link; skips thin/empty findings (no theater).
3. **Session wiring**
   - `LeisureRunner` → `leisure` or `research` (when `research_*` tools ran) after a worth-keeping pass.
   - `AssignedWorkRunner` → `parked-work` on successful B3 budget sessions only.
4. **FED-18.3 APIs**
   - `GET /activities?subject=&limit=` — desk columns (entryNo, occurredAt, activity, subject, desc)
   - `GET /activities/{entryNo}` — single row
   - `GET /journals/topic?subject=` — topic-tagged thoughts crumbs
   - `GET /presence/while-you-were-gone?since=&limit=` — activities + journal crumbs (empty is honest)
5. **`ListSinceAsync`** on `IVictoriaActivitiesLogStore` for the while-you-were-gone window.

## Acceptance

| Criterion | Result |
| --- | --- |
| Leisure/research/parked-work write activities log with non-generic Desc | Pass |
| Topic journal append, 1–2¶ floor, no upper cap | Pass |
| List/read APIs for Identity / Presence / while-you-were-gone | Pass |
| Two sessions same subject, different findings → different Desc/journal | Pass — tests below |

## Out of scope (honored)

Voice recording of journals; outbound ping; FED UI polish.

## Files

- `SoulCore/SoulCore.Inference/Presence/ActivityResidueComposer.cs`
- `SoulCore/SoulCore.Inference/Presence/IActivityResidueWriter.cs`
- `SoulCore/SoulCore.Inference/Presence/ActivityResidueWriter.cs`
- `SoulCore/SoulCore.Memory/IVictoriaActivitiesLogStore.cs` (`ListSinceAsync`)
- `SoulCore/SoulCore.Memory/Repositories/SqliteVictoriaActivitiesLogRepository.cs`
- `SoulCore/SoulCore.Host/Hosting/ServiceCollectionExtensions/CompanionServiceCollectionExtensions.cs`
- `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs`
- `SoulCore/SoulCore.Host/Loop/AssignedWorkRunner.cs`
- `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs`
- `SoulCore/SoulCore.Protocol.Tests/Prop187ActivityResidueTests.cs`

## Evidence (`dotnet test`)

```
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj
  --filter "FullyQualifiedName~Prop187ActivityResidueTests"

Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

Cases: distinct Desc/journal across two finding sets; generic-template reject; journal ¶ floor + long uncapped body; writer persist + journal link; `ListSince` window; thin findings skip.

## Next

FED-18.3 unblocked for Presence / activities desk; PROP-18.2 may consume activity-log crumbs in daybook.
