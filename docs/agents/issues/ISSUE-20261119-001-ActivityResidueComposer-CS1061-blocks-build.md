---
issue_id: ISSUE-20261119-001
discovered: 2026-11-19
severity: P1 (Critical)
status: Fixed
fixed: 2026-11-19
fixed_by: PROP-18.15 (verified) / PROP-18.2 (original SplitParagraphs → List<string>)
report: docs/agents/reports/PROP-18.15-BED01-to-PM01.md
prop_context: PROP-18.11 (discovered while running Prop188IdleParkedBudgetTests)
---

[已修复 2026-11-19] `SplitParagraphs` already returns `List<string>`; Inference build 0 errors; Prop188IdleParkedBudgetTests 7/7 Pass. See PROP-18.15-BED01-to-PM01.md.

## Problem Description

`dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests"` fails at compile time before any tests run. Unrelated compile error in `ActivityResidueComposer.cs` blocks rebuild of `SoulCore.Inference`.

## Reproduction Steps

1. From repo root: `dotnet test SoulCore\SoulCore.Protocol.Tests\SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests"`
2. Observe build failure.

## Expected Result

Solution / Inference project compiles; Prop188 suite executes.

## Actual Result

```
C:\Users\kurtw\.house_victoria\SoulCore.AI\SoulCore\SoulCore.Inference\Presence\ActivityResidueComposer.cs(150,19): error CS1061: 'IReadOnlyList<string>' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'IReadOnlyList<string>' could be found
```

`SplitParagraphs` returns `IReadOnlyList<string>`; code calls `paras.Add(...)` at line 150.

## Impact Scope

Blocks official unit-test gate for PROP-18.8 / any Inference-dependent suite rebuild. QA verified B3 budget via isolated harness + live Host `:7710` (see PROP-18.11 report); official filter still cannot run until this is fixed.

## Fix suggestion

Use a mutable list (e.g. `List<string>`) for the pad loop, or append via concatenation instead of `Add` on `IReadOnlyList`.
