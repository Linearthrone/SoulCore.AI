---
type: report
from: BED-01
to: PM-01
task_id: PROP-18.15
prop_id: PROP-18.15
prop_root: PROP-18-chronology-and-felt-persistence
status: Pass
created: 2026-11-19
issue: docs/agents/issues/ISSUE-20261119-001-ActivityResidueComposer-CS1061-blocks-build.md
---

# PROP-18.15 — ActivityResidueComposer CS1061 — Pass

## Verdict

**Pass**

## Finding

CS1061 was already fixed in-tree. No new code change required this turn.

`SplitParagraphs` returns `List<string>` (mutable), so `paras.Add(...)` at line 150 is valid. The fix landed earlier under PROP-18.2 (documented as “unrelated compile fix: `SplitParagraphs` → `List<string>` so Host builds”). QA’s ISSUE-20261119-001 was filed against a stale rebuild that still had `IReadOnlyList<string>`.

Current signature:

```csharp
private static List<string> SplitParagraphs(string text)
{
    return text
        .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(p => p.Length > 0)
        .ToList();
}
```

## Evidence

### 1. Inference build (CS1061 gone)

```text
dotnet build SoulCore\SoulCore.Inference\SoulCore.Inference.csproj -c Release --no-incremental

Build succeeded.
    3 Warning(s)   # CA1416 VboxConsoleMouse only — unrelated
    0 Error(s)
```

No CS1061 on `ActivityResidueComposer.cs`.

### 2. Prop188 filter green

Default `dotnet test` path hit MSB3027 file-lock on running Host (PID 58832). Rebuilt Protocol.Tests + deps to `%TEMP%\prop188-bed01-verify` (avoids locked Host `bin\`) and ran the filter:

```text
dotnet test "%TEMP%\prop188-bed01-verify\SoulCore.Protocol.Tests.dll" --filter "FullyQualifiedName~Prop188IdleParkedBudgetTests"

Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 151 ms
```

## Issue close-out

`ISSUE-20261119-001` → status **Fixed** (PROP-18.15 / prior PROP-18.2 fix verified 2026-11-19).

## Next

Official Prop188 gate unblocked for PROP-18.8 / QA re-run when Host bin is unlocked (or via alternate `-o` as above).
