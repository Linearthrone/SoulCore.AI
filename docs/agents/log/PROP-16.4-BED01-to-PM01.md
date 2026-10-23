---
type: report
prop_id: PROP-16.4
prop_root: PROP-16-presence-glass-overlay
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-22
updated: 2026-10-22
ticket: docs/agents/tasks/PROP-16.4-PM01-to-BED01.md
proposal: docs/agents/unexecuted_proposals/presence-glass-overlay.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
branch: pr-177
---

# PROP-16.4 — Trait catalog and description draft — Pass

## Verdict

**Pass.** `PersonaTraitScales` is still one object. The original six property names stay, and their property defaults stay `0.5`. The other Top 30 and Top 50 traits are on that same object, using the proposal defaults. Missing JSON keys load at the property initializer.

The compiler snaps with the existing edges (under 0.34, under 0.67, else). It emits at most eight directive lines after `[Persona directives]`. The marker is not one of the eight. If every trait is on its own default, there is no marker and no lines. Off-default traits sort by absolute distance from that trait’s own default. Ties follow the proposal catalog order. Boundary strictness, when it is off default and not omitted, is line 1 and counts toward the eight. A low boundary line is still omitted when the caller passes hard limits, and that omission does not leave an empty slot. Line text does not say “obey”. Compile does not change tool flags.

`PhysicalDescription` is on the pack. Missing JSON stays empty. A save whose trimmed description is longer than 1000 characters throws before the file is written. A trimmed length of 1000 is stored trimmed.

`POST /api/personas/{personaId}/description-draft` calls `CompleteAsync` only. The model is the pack model when it is set, otherwise the Host model. The returned text is cut at 1000 characters. The route does not write the pack, charter, episodic store, or tool flags. A failed model call returns 503 and writes nothing.

No Avalonia edit. No commit. No push. Branch stayed `pr-177`.

## Acceptance

| Line | Result | Evidence |
| --- | --- | --- |
| An old pack JSON without the new keys still loads, and the original six stay at their stored values | Pass | `OldPackJson_MissingNewKeys_KeepsStoredSix_AndNewTraitDefaults`. Stored six match the file (`warmth` 0.81 through `recallBias` 0.72). Every other trait is on its property default. `physicalDescription` is empty. Tool flags and `## LIMITS` stay. |
| A trait moved across 0.34 or 0.67 changes the compiled line; a trait left on default adds no line | Pass | `ToBand_SnapsContinuousScale` and `Compile_CrossingBandEdge_ChangesTheLine_DefaultAddsNone`. Warmth at 0.33 and 0.34 differ, and 0.66 and 0.67 differ. Directness off default emits a line; Warmth left at 0.5 does not. `Compile_AllDefaults_EmitsNoMarkerAndNoLines` returns empty. |
| Fifty traits set far from default still produce at most eight directive lines | Pass | `Compile_FiftyOffDefault_AtMostEightLines_BoundaryFirst`. All 50 set to 0. Exactly 8 lines. Line 1 is boundary strictness. The next seven are the furthest from their own defaults, ties in catalog order. |
| A blurb that contains `## LIMITS` is unchanged by compile, and the block does not grant tools or say “obey” | Pass | `Compile_HardLimits_LeavesBlurbAndToolFlags_AndDoesNotSayObey`. Blurb text and `allowDesktop` / `allowBrowser` / `allowEmail` are unchanged. The block has no “obey”, no “grant”, and no `allowDesktop`. Low boundary “Flexible boundaries” is omitted (`Compile_HardLimitsFromBlurb_OmitsFlexibleBoundaries`). Mid 0.40 and high 0.95 still emit (`Compile_HardLimitsFromBlurb_KeepsMidAndHighBoundaries`). |
| Description draft returns text and leaves pack bytes, charter, and episodic rows unchanged | Pass | `DescriptionDraft_PackModel_ReturnsTruncatedText_WritesNothing`. A 1200-character model reply is returned at 1000 characters. `pack.json` bytes match. Charter anchors match. Episodic recall matches. Tool flags and `## LIMITS` stay. `CompleteWithToolsAsync` was not called. Pack model `pack-draft:7b` was passed to `CompleteAsync`. |
| Model-down returns 503 and writes nothing. A description over 1000 characters is rejected on save | Pass | `DescriptionDraft_ModelDown_Is503_WritesNothing`: `HttpRequestException` → 503 `chat.model_down`, pack bytes, charter, and episodic rows unchanged. `PhysicalDescription_Over1000_Rejected_Trimmed1000_Saves`: 1001 after trim throws before write and the previous file bytes stay; 1000 after trim is stored as 1000 characters. |
| `dotnet test` for the new and updated trait tests passes, with the output in the report | Pass | 31 passed, 0 failed. Output below. |
| Report lists commands and results | Pass | This file. |

## What changed

- `PersonaTraitScales` gained the rest of the Top 30 and the Top 50 on the same object. Original six defaults stay `0.5`. New defaults match the proposal (for example verbosity `0.45`, caution `0.55`, loyalty to the operator `0.70`, metaphor use `0.35`).
- `PersonaTraitCatalog` is the single order used by the compiler and by sparse trait merge. It is not a second stored list.
- `PersonaTraitCompiler.Compile` replaced the old always-on voice / boundary / recall block. Band edges are unchanged. Cap is eight lines. Blank starter values for formality and playfulness stay `0.4`, so those two are off the `0.5` property default and still emit when a blank pack is compiled.
- `PhysicalDescription` on the pack. `PersonaPackStore.UpsertAsync` rejects a trimmed length over 1000 before any write and stores the trimmed text otherwise. Sparse PUT accepts `physicalDescription`.
- `POST /api/personas/{personaId}/description-draft`. Optional JSON `{"notes":"..."}` or a JSON string. `CompleteAsync` only, with the resolved model passed in. `IInferenceClient` has a 5-argument `CompleteAsync` whose default implementation keeps old callers on the 4-argument method. Ollama uses the explicit model when it is set, and otherwise keeps pack-then-Host resolution.
- Tests updated for the old three-line compiler, plus old-JSON load, description length, and the draft route.

`ChatContextBuilderTests` was also run in this session (identity block still sees the marker when a blank or mentor pack is off default, and low “Flexible boundaries” stays omitted when the blurb has `## LIMITS`). That run was 44 passed, 0 failed, including the 31 tests below.

## Commands

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --nologo --filter "FullyQualifiedName~PersonaTraitCompilerTests|FullyQualifiedName~PersonaPackMergeTests|FullyQualifiedName~PersonaDescriptionDraftTests"
```

Branch during the run: `pr-177` (`git branch --show-current`).

### Trait, pack-merge, and description-draft tests

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
  SoulCore.Adapters.Ws -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Adapters.Ws\bin\Debug\net8.0\SoulCore.Adapters.Ws.dll
  SoulCore.Memory -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Memory\bin\Debug\net8.0\SoulCore.Memory.dll
  SoulCore.Inference -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Inference\bin\Debug\net8.0\SoulCore.Inference.dll
  SoulCore.Hermes -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Hermes\bin\Debug\net8.0\SoulCore.Hermes.dll
  SoulCore.Host -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll
  SoulCore.Protocol.Tests -> C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll
Test run for C:\Users\kurtw\OneDrive\Apps\SoulCore.AI_CreatorEdition\SoulCore\SoulCore.Protocol.Tests\bin\Debug\net8.0\SoulCore.Protocol.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    31, Skipped:     0, Total:    31, Duration: 686 ms - SoulCore.Protocol.Tests.dll (net8.0)
```
