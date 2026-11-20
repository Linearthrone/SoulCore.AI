---
type: report
prop_id: PROP-20.10
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.10
status: findings
created: 2026-11-17
related_qa: docs/agents/reports/PROP-20.9-QA01-to-PM01.md
---

# SLOP-01 Report — PROP-20.10 (Wave A)

## Scope

- Related QA task / report: `PROP-20.9-QA01-to-PM01.md` (Pass; Ready for SLOP-01)
- Files / packages scanned:
  - `SoulCore/SoulCore.Core/VictoriaMindBlock.cs`
  - `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs`, `MemoryTape.cs`
  - `SoulCore/SoulCore.Host/Companion/` (`SmsInboundService`, `SmsOutboundComposer`, `SmsOutboundService`, `VictoriaMindBlockBuilder`, `IVictoriaMindBlockBuilder`, `SmsToolGate` consumers)
  - `SoulCore/SoulCore.Host/Loop/LeisureRunner.cs`
  - `SoulCore/SoulCore.Inference/Presence/` (`SmsToolGate`, `LeisureMindBlock`, `LeisureTools`, `UntrustedObservation`)
  - `SoulCore/SoulCore.Memory/` (dated recall API, migration `012` + Schema companion)
  - `House/House.ChatDesktop/` (Identity client, `SoulCoreJournalClient`, Her journal window)
- Method: read DEV/SEC/FED/QA reports for change surface → read primary sources → ripgrep for aliases (`SmsTools`, `MayWriteAsHer`, `BuildMemoryBlock`, truncate helpers, journal clients)

**Read-only.** No code modified.

## Summary

- Findings count: **9**
- Highest severity: **P1**
- Verdict: **findings** (not clean)

## Findings

### F1 — Dead `SmsTools` compatibility alias

- Category: alias-sprawl
- Severity: **P1**
- Evidence:
  - `SoulCore/SoulCore.Inference/Presence/SmsToolGate.cs` lines 120–134 — `public static class SmsTools` forwards `InboundUsesToolLoop` / `IsAllowed` / `FilterDefinitions` / `FilterRegistry` to `SmsToolGate`
  - Product call sites use `SmsToolGate` only (`SmsInboundService` ~207: `SmsToolGate.FilterRegistry`)
  - Ripgrep: `SmsTools` appears in **no** `.cs` call sites outside its own definition; only PROP-20.5 / 20.7 reports mention it
- Why it matters: Same-purpose second name with zero consumers invites future drift (security gate edited in one place, callers of the other never updated).
- Recommended action: **remove**
- Suggested owner: SEC-01 (owns gate) or BED-01 if PM prefers product-layer cleanup

### F2 — Triple SMS ellipsis-truncate helpers

- Category: duplicate
- Severity: **P1**
- Evidence:
  - `SmsOutboundComposer.Truncate` (private, ~178–185) — `s[..(max-1)].TrimEnd() + "…"`
  - `SmsInboundService.TruncateForSms` (internal, ~531–536) — same pattern (+ null/empty guard)
  - `SmsInboundService.Truncate` (private, ~538–539) — same pattern for memory/paper titles
  - `SmsOutboundService` ~59 / ~103 calls **`SmsInboundService.TruncateForSms`** for carrier safety (outbound depending on inbound type)
- Why it matters: Copy-paste truncate + cross-type dependency; a budget/edge-case fix can land in one helper and miss the others.
- Recommended action: **dedupe** — one public/internal helper on `SmsOutboundComposer` (e.g. `TruncateForCarrier`); inbound/outbound both call it; drop the private twin if still redundant.
- Suggested owner: BED-01

### F3 — `MemoryTape.StripUntrusted` copy-pastes `UntrustedObservation.StripFromPreamble`

- Category: duplicate
- Severity: **P1**
- Evidence:
  - `UntrustedObservation.StripFromPreamble` (`UntrustedObservation.cs` ~22–45) — first-marked skip loop over `IReadOnlyList<string>`
  - `MemoryTape.StripUntrusted` (`MemoryTape.cs` ~165–189) — same control flow over `MemoryRecallLine`, gating on `UntrustedObservation.IsMarked(Content)`
  - Chat dated path uses `MemoryTape.StripUntrusted`; string preamble overload still uses `UntrustedObservation.StripFromPreamble` (`ChatContextBuilder.cs` ~125 vs ~155)
- Why it matters: Two strip algorithms can diverge (e.g. case/marker rules); Wave A already centralized marking in `UntrustedObservation`.
- Recommended action: **dedupe** — project contents → `StripFromPreamble` → rebuild lines, or extract one shared filter predicate/loop.
- Suggested owner: BED-01

### F4 — `MayWriteAsHer` is a same-purpose alias of `ContainsRequiredMind`

- Category: alias-sprawl
- Severity: **P2**
- Evidence:
  - `LeisureMindBlock.cs` ~88: `MayWriteAsHer(systemPrompt) => ContainsRequiredMind(systemPrompt)`
  - `LeisureRunner.cs` ~109 checks `ContainsRequiredMind(mindBlock)`; ~151 / ~214 check `MayWriteAsHer(system)` for the same gate
- Why it matters: Two names for one boolean rule; readers must chase both when hardening the write-as-her gate.
- Recommended action: **dedupe** — keep one name (prefer `MayWriteAsHer` at runner boundary *or* `ContainsRequiredMind` everywhere); delete the other.
- Suggested owner: BED-01

### F5 — Mutable static `SharedBuilder` test hook with silent catch

- Category: slop
- Severity: **P2**
- Evidence:
  - `LeisureMindBlock.cs` ~38 `SharedBuilder` get/set; ~49–51 preferred before `VictoriaMindBlock.Build`; ~161–172 `TrySharedBuilder` swallows all exceptions with empty `catch`
  - Production `LeisureRunner` never assigns `SharedBuilder`; only `Prop17LeisureTests.LeisureMindBlock_SharedBuilderHook_PreferredWhenValid` sets it
  - Soft-dep already exists: `IVictoriaMindBlockBuilder` / `VictoriaMindBlock.Build`
- Why it matters: Global mutable hook + silent failure is AI-style test scaffolding left in product surface; can mask broken builders in future DI wiring.
- Recommended action: **remove** (tests call `VictoriaMindBlock.Build` / inject floors directly) — or **ask-user** if PM wants to keep a deliberate extension point.
- Suggested owner: BED-01
- Ask-user question (if ask-user): Keep a leisure-only extension hook, or rely solely on `VictoriaMindBlock` / DI builder?

### F6 — Leisure constant re-exports + test-only `BuildTemporaryMidDefaults`

- Category: alias-sprawl
- Severity: **P2**
- Evidence:
  - `LeisureMindBlock.cs` ~16–28: `FloorsHeader` / `RestingHeader` / `ProvisionalMidWeight` / `ProvisionalLedgerReason` alias `VictoriaMindBlock.*`
  - `BuildTemporaryMidDefaults` (~60–66) builds via `VictoriaMindBlock.Build(ProvisionalFloors)` + leisure append — same core path as `Build` → `BuildSharedFloorsAndResting` when floors empty
  - Ripgrep: `BuildTemporaryMidDefaults` used only in `Prop17LeisureTests`
- Why it matters: Extra names for shared markers; temporary builder duplicates the default `Build` path.
- Recommended action: **dedupe** (use `VictoriaMindBlock` markers in tests; drop or inline `BuildTemporaryMidDefaults`) — **ask-user** if leisure wants a stable public façade separate from Core.
- Suggested owner: BED-01
- Ask-user question: Should leisure keep its own marker constants for API stability, or always import `VictoriaMindBlock`?

### F7 — Legacy `[Resting]` marker still accepted

- Category: slop
- Severity: **P2**
- Evidence:
  - `VictoriaMindBlock.RestingMarker` = `"[Resting traits]"` (`VictoriaMindBlock.cs` ~14)
  - `LeisureMindBlock.ContainsRequiredMind` (~75–76) also accepts `prompt.Contains("[Resting]", …)` as alternate
  - No Wave A emitter found for bare `[Resting]` (builder emits RestingMarker only)
- Why it matters: Dead compatibility branch weakens the gate (any prompt containing the substring `[Resting]` could pass the resting-header check without the traits section).
- Recommended action: **ask-user** — if no pre-20.4 prompts persist, **remove** the legacy branch; if old stored prompts exist, document and keep.
- Suggested owner: BED-01
- Ask-user question: Are any persisted leisure/system prompts still using `[Resting]` without `traits`?

### F8 — `ChatContextBuilder.BuildMemoryBlock` thin passthroughs

- Category: alias-sprawl
- Severity: **P2**
- Evidence:
  - `ChatContextBuilder.cs` ~369–381: both overloads are expression-bodied delegates to `MemoryTape.BuildMemoryBlock`
  - Tests (`ChatContextBuilderTests`) call `ChatContextBuilder.BuildMemoryBlock`, not `MemoryTape`
- Why it matters: Dual entry points for the same tape logic; mild, but PROP-20.2 already extracted `MemoryTape` to avoid collisions.
- Recommended action: **ask-user** — keep façade for chat API stability, or point tests/callers at `MemoryTape` and remove wrappers.
- Suggested owner: BED-01
- Ask-user question: Prefer chat-facing `BuildMemoryBlock` façade, or MemoryTape-only API?

### F9 — `IVictoriaMindBlockBuilder` XML overclaims “related memories”

- Category: slop
- Severity: **P2**
- Evidence:
  - `IVictoriaMindBlockBuilder.cs` ~3–5: summary says “floors + resting + related memories”
  - `VictoriaMindBlockBuilder.TryBuildAsync` only loads charter floors + `VictoriaMindBlock.Build` (no related memories)
  - Related memories are appended only in `LeisureMindBlock.AppendLeisureContext`
- Why it matters: Doc/implementation mismatch steers future SMS/leisure callers to expect memories that never appear on this interface.
- Recommended action: **remove** the “related memories” claim from the interface summary (doc-only fix).
- Suggested owner: BED-01

## Clean areas

- **Shared mind path:** Desk (`ChatContextBuilder`), SMS (`IVictoriaMindBlockBuilder`), and leisure (`LeisureMindBlock` → `VictoriaMindBlock.Build`) correctly share Core floors/resting — no second pasted Mid trait table in Host.
- **Tool gates:** `SmsToolGate` vs `LeisureTools` are intentional parallel allow/deny lists (different surfaces); not same-purpose aliases.
- **Desk journals:** `SoulCoreJournalClient` + Her journal vs `SoulCoreResearchClient` + Research window are purposeful splits (PROP-20.8).
- **Memory API:** `Recall*Async` string projections wrapping `Recall*DatedAsync` are documented compatibility, not accidental sprawl.
- **Migration 012:** `Migrations/012_*.sql` is runtime DDL; `Schema/012_*.sql` is docs-only companion (DBD intentional) — not a duplicate embed.

## Notes for PM

- Safe to archive? **no** — disposition F1–F3 (P1) at minimum before treating Wave A hygiene as done; F4–F9 are focused cleanup / ask-user.
- If findings: prefer DEV/SEC cleanup tickets citing F-IDs; re-enter OPS→QA only if cleanup touches SMS gate or leisure write-as-her behavior.
- QA Pass (PROP-20.9) stands; this audit does not reopen functional Fail.
