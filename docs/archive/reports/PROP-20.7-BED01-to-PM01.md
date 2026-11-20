---
type: report
prop_id: PROP-20.7
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
status: Pass
created: 2026-11-17
---

# PROP-20.7 — SMS reply = answer + public URL; long findings → research paper — **Pass**

## Verdict

Outbound SMS product pipe is live: short answer + public source URL(s) within the **480-char** cut; overflow findings persist as a **research paper** (not private journals). Consumes **PROP-20.5** `SmsToolGate` (already merged in-tree). Soft-depends **PROP-20.4** mind block via `IVictoriaMindBlockBuilder` (registered; private journals never enter SMS preamble). Did **not** rewrite `SmsSecurityGateTests`.

## Done

1. **`SmsOutboundComposer`** — shapes carrier body (answer + public http(s) URLs), strips loopback/private/internal paper addresses, hard-caps at 480, returns `LongFindingsForPaper` when truncated.
2. **`SmsInboundService`** — when `IToolRegistry` is wired: filtered tool loop through `SmsToolGate.FilterRegistry`; collects `browser_navigate` public URLs; composes outbound; upserts research paper for long findings; attaches 20.4 mind block when builder resolves.
3. **DI** — factory wires `toolRegistry` + `research` + optional `mindBlock` (20.4 builder already registered).
4. **`SmsOutboundService`** — still enforces 480 via `SmsOutboundComposer.MaxSmsChars` as final carrier safety.
5. Tests for truncation, URL inclusion, paper persist; left SEC gate suite untouched and green.

## Acceptance

| Criterion | Result |
| --- | --- |
| Outbound SMS ≤480 includes answer + public source URL when research opened a page | **Pass** |
| Long body → research paper store, not private journal books | **Pass** |
| Evidence + this report | **Pass** |

## Evidence

```
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj \
  --filter "FullyQualifiedName~SmsOutboundComposer|FullyQualifiedName~SmsInboundProp207|FullyQualifiedName~SmsSecurityGate|FullyQualifiedName~SmsInboundServiceTests|FullyQualifiedName~SmsOutboundServiceTests"

Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 149 ms
```

Key cases:

- `Compose_AnswerPlusPublicUrl_Within480`
- `Compose_LongAnswer_TruncatesAndFlagsPaper`
- `Compose_StripsInternalPaperAddress_KeepsPublicUrl`
- `Inbound_ToolNavigate_OutboundIncludesPublicUrl`
- `Inbound_LongFindings_PersistsResearchPaper_NotJournal`
- Full `SmsSecurityGateTests` (20.5) still green — not rewritten

## Files

| Path | Change |
| --- | --- |
| `SoulCore.Host/Companion/SmsOutboundComposer.cs` | **new** — 480 pipe + URL collect/normalize |
| `SoulCore.Host/Companion/SmsInboundService.cs` | tool-loop reply shaping + paper persist + mind soft-dep |
| `SoulCore.Host/Companion/SmsOutboundService.cs` | 480 cut via composer constant |
| `SoulCore.Host/Companion/IVictoriaMindBlockBuilder.cs` | soft-dep contract (shared with 20.4) |
| `SoulCore.Host/Hosting/.../CompanionServiceCollectionExtensions.cs` | inbound factory wiring |
| `SoulCore.Protocol.Tests/SmsOutboundComposerTests.cs` | **new** composer + inbound product tests |

## Dependencies / retest notes

- **20.5**: Consumed in-tree `SmsToolGate` / `SmsTools` alias — **no retest gap**. Gate tests left as SEC owns them; suite still Pass.
- **20.4**: Mind block builder registered; SMS preamble gets `[Mind]` when `TryBuildAsync` succeeds. No private `thoughts`/`wants`/`dreams` in SMS preamble.
- Out of scope (per ticket): MMS images/documents as research send.

## Handoff

Ready for PM accept / QA smoke on live SMS + browser_navigate → texted public URL.
