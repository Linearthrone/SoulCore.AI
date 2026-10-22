---
type: report
prop_id: PROP-15.20
prop_root: PROP-15-persona-creation-platform
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-05
updated: 2026-10-05
depends_on: PROP-15.13c (Fail), PROP-15.17 (Pass)
qa_report: docs/agents/reports/PROP-15.13c-QA01-to-PM01.md
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Hard-policy coda after full preamble
---

# PROP-15.20 — Hard-policy coda — **Pass**

## Verdict

**Pass.** When `BlurbHasHardLimits`, Host appends a fixed `[Hard policy]` coda **after** full preamble assembly (identity + memory + emotion + all tool-loop guidance). IdentityBlurb / charter unchanged. Unit tests prove order: coda after `[Tools]` / `[Persona tools]`, and preamble ends on the coda.

## Fix

### Placement (`ChatContextBuilder.BuildAsync`)

After `BuildContextPreamble` and (when tool-loop) all `AppendToPreamble` / `AppendToolGuidance` calls:

```text
preamble = PersonaPromptBlocks.AppendHardPolicyCoda(preamble, pack);
```

Runs on both tool-loop and non-tool paths. No-op when blurb lacks `LIMITS`.

### Coda (`PersonaPromptBlocks`)

Host-owned fixed template (`HardPolicyMarker = "[Hard policy]"`) — not a charter paste. Restates only:

1. Refuse invented version/flag/release facts — probe, don’t narrate  
2. Unauthorized third-party / nostalgia-auth → one dry line + lab/auth path (no method, no essay)  
3. Never paste IdentityBlurb / system seed verbatim  
4. Never affirm offense against non-owned / staging-shaped infra  

Idempotent append. Soft tool/agency guidance left intact (coda alone suffices for unit order proofs; no “be helpful” gate this slice).

### Stress model note

`mother:latest` is uncensor-aggressive GGUF — coda is **Host enforcement** at attention’s end. PM may retarget pack model separately; this PROP does not change `inferenceModel`.

## Files changed

### Modified
- `SoulCore/SoulCore.Core/Persona/PersonaPromptBlocks.cs` — `HardPolicyMarker`, `BuildHardPolicyCoda`, `AppendHardPolicyCoda`
- `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` — append coda after full preamble
- `SoulCore/SoulCore.Protocol.Tests/ChatContextBuilderTests.cs` — order + no-op tests
- `docs/agents/tasks/PROP-15.20-PM01-to-BED01.md` — status → Pass

## Evidence

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj -c Release --filter "FullyQualifiedName~ChatContextBuilder"

Passed!  - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 80 ms
```

(Stopped Release Host PID 4808 briefly so DLLs could copy — same unlock pattern as 15.17/15.19.)

### Order proof (tests)

| Test | Asserts |
| --- | --- |
| `AppendHardPolicyCoda_WhenLimits_AppendsMarkerOnce` | Marker after `[Tools]`; idempotent |
| `AppendHardPolicyCoda_WithoutLimits_IsNoOp` | No marker |
| `BuildAsync_WhenBlurbHasLimits_HardPolicyCodaAfterToolGuidance` | `[Hard policy]` after `[Tools]` + `[Persona tools]`; preamble **ends** on coda last line |
| `BuildAsync_WithoutLimits_OmitsHardPolicyCoda` | Blank pack → no coda |

## Ops note

Release Host was stopped for DLL unlock. **OPS-HOME PROP-15.20b** should recycle Release Host on `:7700`, re-pin lexi / `mother:latest`, then QA 15.13d.

## Ask PM

1. Accept PROP-15.20; archive when ready.  
2. Dispatch OPS-HOME 15.20b (Host recycle + lexi pin).  
3. Re-dispatch QA-01 as **15.13d** stress after pin — do not treat unit Pass as Ready for SLOP-01 until suite scores.  
4. Optional: if A\*/B\*/S3 still Fail under `mother:latest`, consider pack model retarget (Host coda already applied).
