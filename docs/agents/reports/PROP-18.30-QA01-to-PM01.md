---
type: report
prop_id: PROP-18.30
prop_root: PROP-18-chronology-and-felt-persistence
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
upstream: docs/agents/reports/PROP-18.29-BED01-to-PM01.md
task: docs/agents/tasks/PROP-18.30-PM01-to-QA01.md
depends_on: PROP-18.29
---

# PROP-18.30 — QA ActivityLogDto desc-only + Composer.CountParagraphs — Pass

## Result

**Pass.** PROP-18.29 residue hygiene verified: wire DTO/client emit `desc` only (no `sessionDesc` alias); Writer depth-floor gate reuses `ActivityResidueComposer.CountParagraphs`. Prop187 and SoulCoreActivitiesClient suites both green (6/6 each).

**Ready for SLOP-01.**

## Acceptance matrix

| Criterion | Result | Evidence |
| --- | --- | --- |
| Host `ActivityLogDto` wire field `desc` only (no `sessionDesc`) | **Pass** | `WebApplicationExtensions.cs` ~1297–1308: anonymous object has `desc = e.SessionDesc` only; comment documents PROP-18.29 F1 |
| FED `SoulCoreActivitiesClient` maps `desc` only | **Pass** | `ActivityDto` has `[JsonPropertyName("desc")] Desc` only; `ToActivityRow` uses `a.Desc`; no `SessionDesc` property |
| Desk fixtures omit `sessionDesc` | **Pass** | `SoulCoreActivitiesClientTests` JSON uses `"desc":"..."` only |
| Writer uses `Composer.CountParagraphs` for ¶ floor | **Pass** | `ActivityResidueWriter.cs` ~68: `ActivityResidueComposer.CountParagraphs(journalBody)` |
| `CountParagraphs` public + same split as compose | **Pass** | `ActivityResidueComposer.cs` ~233: `CountParagraphs` → `SplitParagraphs` |
| Prop187 tests green | **Pass** | 6/6 below |
| SoulCoreActivitiesClient tests green | **Pass** | 6/6 below |

## Source checks (QA read-only)

### F1 — desc only

- Host DTO: `desc = e.SessionDesc` — store property name unchanged; wire name is `desc`.
- Client: no `sessionDesc` / `SessionDesc` hedge on `ActivityDto` or row mapping.
- Grep of desk client + tests: zero wire `sessionDesc` references.

### F2 — Composer reuse

- Writer depth gate calls `ActivityResidueComposer.CountParagraphs` (not a twin `"\r\n\r\n"` / `"\n\n"` re-split).
- Composer exposes `public static int CountParagraphs(string text) => SplitParagraphs(text).Count`.

## Evidence (`dotnet test`)

Workspace: `c:\Users\kurtw\.house_victoria\SoulCore.AI`  
Date: 2026-11-19

### Prop187 (SoulCore.Protocol.Tests)

Command:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests --filter "FullyQualifiedName~Prop187" --no-restore
```

Output:

```text
  SoulCore.Protocol -> ...\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  SoulCore.Core -> ...\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  SoulCore.Config -> ...\SoulCore.Config\bin\Debug\net8.0\SoulCore.Config.dll
  SoulCore.Memory -> ...\SoulCore.Memory\bin\Debug\net8.0\SoulCore.Memory.dll
  SoulCore.Adapters.Ws -> ...\SoulCore.Adapters.Ws\bin\Debug\net8.0\SoulCore.Adapters.Ws.dll
  SoulCore.Inference -> ...\SoulCore.Inference\bin\Debug\net8.0\SoulCore.Inference.dll
  SoulCore.Hermes -> ...\SoulCore.Hermes\bin\Debug\net8.0\SoulCore.Hermes.dll
  SoulCore.Host -> ...\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll
  SoulCore.Protocol.Tests -> ...\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll
Test run for ...\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 1 s - SoulCore.Protocol.Tests.dll (net8.0)
```

### SoulCoreActivitiesClient (House.ChatDesktop.Tests)

Command:

```text
dotnet test House/House.ChatDesktop.Tests --filter "FullyQualifiedName~SoulCoreActivitiesClient" --no-restore
```

Output:

```text
  SoulCore.Protocol -> ...\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  House.ChatDesktop -> ...\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.dll
  House.ChatDesktop.Tests -> ...\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll
Test run for ...\House.ChatDesktop.Tests\bin\Debug\net8.0\House.ChatDesktop.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 25 ms - House.ChatDesktop.Tests.dll (net8.0)
```

## Notes for PM

- Internal store / domain still use `session_desc` / `SessionDesc` — expected; QA scope was wire + Writer reuse only.
- No new issues filed.
- Next: dispatch **SLOP-01** on the PROP-18.29 change set.
