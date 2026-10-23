---
type: report
prop_id: PROP-16.3
prop_root: PROP-16-presence-glass-overlay
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.3-PM01-to-BED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.3 — Archive write — Pass

## Verdict

**Pass.** `archive` is stored as `archive` and is not quarantined. `POST /api/personas/{personaId}/archive` writes that persona’s episodic store with `WriteEpisodicAsync(summary, "archive")` while another persona stays active. Unknown id is 404. Blank text and text longer than 2000 characters after trim are 400. Charter, LIMITS, and tool flags are not written.

No model call. No Avalonia edit. No chat-thread hide. No commit. No push. Branch stayed `pr-177`.

Body is JSON `{"summary":"..."}` or a JSON string. The handler trims, then rejects length over 2000. It resolves the pack first, then `IPersonaStoreHub.GetMemoryStore(personaId)`. It does not use `PersonaScopedMemoryStore`.

SQLite’s episodic CHECK had to grow or the insert would fail. Migration `008_episodic_source_archive` rebuilds that CHECK and adds `archive`. Quarantine stays `imported` only.

`GetMemoryStore` now opens the named persona’s file when that persona is not the active bundle. Charter, journal, and the other hub accessors still refuse a non-active id. `PersonaStoreHubQuarantineTests` was updated for that memory lookup and still passed.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| A row written for persona A with source `archive` is stored as `archive`, not `system` | Pass | `WriteEpisodic_SourceArchive_PersistsAsArchive_AndIsNotQuarantined` and both HTTP writes. SQL `source` is `archive`. Mixed-case `Archive` is stored as `archive`. |
| That row is readable from A’s store and absent from persona B’s store | Pass | `ArchiveEndpoint_WritesPersonaA_WhileBIsActive_AndDoesNotTouchCharterOrFlags`. A’s `RecallRecentAsync` contains the summary. B’s list is empty. |
| The write still hits A when B is the active persona | Pass | Same test, and `ArchiveEndpoint_Over2000_Is400_AndTrimmed2000_WritesArchiveOnA`. `hub.ActivePersonaId` stays B. The row is in A’s database path. |
| Unknown persona is 404. Blank and over-2000-character summaries are 400 | Pass | `ArchiveEndpoint_UnknownPersona_Is404` (no database file created). `ArchiveEndpoint_BlankSummary_Is400` for `{}`, `""`, whitespace, null, and invalid JSON. `ArchiveEndpoint_Over2000_Is400_AndTrimmed2000_WritesArchiveOnA`: 2001 after trim is 400 and writes nothing; 2000 after trim is 200 and stores 2000 characters. |
| Charter, LIMITS, and tool flags are unchanged | Pass | Same cross-persona test. `pack.json` bytes are unchanged, including `## LIMITS` and `allowDesktop` / `allowBrowser` / `allowEmail`. Charter anchors before and after match. The summary is not a charter row. |
| `dotnet test` for the new tests passes, and the command output is in the report | Pass | 5 passed, 0 failed. Output below. |
| Report lists commands and results | Pass | This file. |

## What changed

- `MemorySourceNormalizer` allows `archive`. Unknown labels still become `system`.
- Migration 008 and the fresh episodic CHECK include `archive`. `is_quarantined` is still set only for `imported`.
- `POST /api/personas/{personaId}/archive` on the existing persona group.
- `GetMemoryStore(personaId)` can open that persona’s SQLite file without switching the active bundle. The active bundle is still the one chat and the scoped facade use.
- Tests: `SoulCore/SoulCore.Protocol.Tests/PersonaArchiveTests.cs`.

## Commands

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --nologo --filter FullyQualifiedName~PersonaArchiveTests
```

Branch during the run: `pr-177` (`git branch --show-current`).

Also run after the hub change, not part of the acceptance filter:

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --nologo --filter "FullyQualifiedName~PersonaStoreHubQuarantineTests|FullyQualifiedName~SqliteMemoryStoreSourceModelTests"
```

Result: Passed. Failed: 0, Passed: 7, Skipped: 0, Total: 7.

### New tests

```text
  Determining projects to restore...
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-gwg2-r3hj-4w44 [C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j3p4-wp97-rph4 [C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j9gm-c75j-xc9q [C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-jjfr-hcj7-qf5w [C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wmxv-xphr-5c9g [C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj]
  All projects are up-to-date for restore.
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-gwg2-r3hj-4w44
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j3p4-wp97-rph4
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-j9gm-c75j-xc9q
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1903: Package 'SixLabors.ImageSharp' 3.1.12 has a known high severity vulnerability, https://github.com/advisories/GHSA-jjfr-hcj7-qf5w
C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj : warning NU1902: Package 'SixLabors.ImageSharp' 3.1.12 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wmxv-xphr-5c9g
  SoulCore.Protocol -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol\bin\Debug\net8.0\SoulCore.Protocol.dll
  SoulCore.Core -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Core\bin\Debug\net8.0\SoulCore.Core.dll
  SoulCore.Config -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Config\bin\Debug\net8.0\SoulCore.Config.dll
  SoulCore.Memory -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Memory\bin\Debug\net8.0\SoulCore.Memory.dll
  SoulCore.Adapters.Ws -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Adapters.Ws\bin\Debug\net8.0\SoulCore.Adapters.Ws.dll
  SoulCore.Inference -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\bin\Debug\net8.0\SoulCore.Inference.dll
  SoulCore.Hermes -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Hermes\bin\Debug\net8.0\SoulCore.Hermes.dll
  SoulCore.Host -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll
  SoulCore.Protocol.Tests -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll
Test run for C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 1 s - SoulCore.Protocol.Tests.dll (net8.0)
```
