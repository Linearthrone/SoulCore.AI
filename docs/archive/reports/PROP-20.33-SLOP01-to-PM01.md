---
type: report
prop_id: PROP-20.33
prop_root: PROP-20-victoria-mind-path
from: SLOP-01
to: PM-01
task_id: PROP-20.33
status: findings
created: 2026-11-18
related_qa: docs/agents/reports/PROP-20.32-QA01-to-PM01.md
related_bed: docs/agents/reports/PROP-20.30-BED01-to-PM01.md
---

# SLOP-01 Report — PROP-20.33

## Scope
- Related QA task / report: PROP-20.32 Pass (`docs/agents/reports/PROP-20.32-QA01-to-PM01.md`); change surface PROP-20.30 Host context-trait framework
- Files / packages scanned:
  - `SoulCore/SoulCore.Core/VictoriaMindBlock.cs` (`SelectContextTraits`, catalog/kind, `ScoreContextAppliesWhen`, `[Context traits]` append)
  - `SoulCore/SoulCore.Host/Ws/ChatContextBuilder.cs` (desk wire + emotion label)
  - `SoulCore/SoulCore.Host/Companion/VictoriaMindBlockBuilder.cs` (SMS soft-dep wire)
  - `SoulCore/SoulCore.Inference/Presence/LeisureMindBlock.cs` (leisure wire)
  - `SoulCore/SoulCore.Host/Hosting/WebApplicationExtensions.cs` (`restingTraits` / `contextTraits` / `contextTraitCatalog`)
  - Spot-check: `SoulCore/SoulCore.Protocol.Tests/VictoriaMindBlockTests.cs`, `ChatContextBuilderTests.cs`, `Prop17LeisureTests.cs` (evidence only; no FED Identity client rewrite in this ticket)
- Method: read linked BED/QA reports; ripgrep `SelectContextTraits|ContextTrait|restingTraits|ScoreApplies|LoadEmotionLabel`; focused read of selection + three consumers + Identity JSON mapping. No product code edits. Bond/soak out of scope.

## Summary
- Findings count: 2
- Highest severity: P2
- Verdict for PM: **findings** (non-blocking hygiene; no P0/P1 alias or divergent path)

## Findings

### F1 — Desk path double-reads emotion for context-trait flat filter
- Category: duplicate
- Severity: P2
- Evidence:
  - `ChatContextBuilder` starts `LoadEmotionPreambleSafeAsync` in parallel (`_emotion.GetAsync` → `EmotionInfluencePrompt.BuildPreamble`) around lines 70–72 / 259–267
  - After `Task.WhenAll`, PROP-20.30 adds a second `LoadEmotionLabelSafeAsync` (`_emotion.GetAsync` again → `DescribeLabel`) around lines 83–87 / 276–288
  - `BuildPreamble` already computes the same label internally (`EmotionInfluencePrompt.cs` lines 20–21) but does not return it to the caller
- Why it matters: same trusted store hit twice per desk turn; label for `IsFlatAffect` can drift if emotion mutates between reads (unlikely but unnecessary). Extra helper is removable once label is derived from the preamble path’s snapshot.
- Recommended action: dedupe
- Suggested owner if remove/dedupe: BED-01
- Ask-user question (if ask-user): —

### F2 — Near-identical applies-when scorers for Compass vs context traits
- Category: duplicate
- Severity: P2
- Evidence:
  - `VictoriaMindBlock.ScoreAppliesWhen` (private, ~454–475): title hit +2, each matching tag +1
  - `VictoriaMindBlock.ScoreContextAppliesWhen` (private, ~477–496): name hit +2, each matching `AppliesWhen` tag +1
  - Bodies differ only by tag source shape (title → dictionary vs row.Name / row.AppliesWhen)
- Why it matters: two copies of the same scoring rule; a future tag-weight tweak can diverge Compass vs context selection silently.
- Recommended action: dedupe (shared private helper, e.g. score name/title + tag list)
- Suggested owner if remove/dedupe: BED-01
- Ask-user question (if ask-user): —

## Clean areas
- Single public selection API: only `SelectContextTraits` (no `Pick*` / `Choose*` / parallel Host selectors). Desk / SMS / leisure all call it.
- Kind filter + 24h fresh gate live inside `SelectContextTraits` / `IsContactFresh` / `IsFlatAffect` — not reimplemented per consumer.
- `[Context traits]` omit-when-empty mirrors Compass by design; second cap in `AppendContextTraits` matches `AppendCompass` pattern (intentional, not alias sprawl).
- Identity fields are purpose-distinct: `restingTraits` (always-on), `contextTraits` (turn-scoped, empty on bare GET), `contextTraitCatalog` (seed for FED) — not same-purpose renames.
- No `any`-style casts, no abnormal try/catch unique to this surface beyond existing soft-dep patterns in `VictoriaMindBlockBuilder`.
- PROP-20.30 comments are contract/PROP markers consistent with surrounding mind-block style, not restating-code noise.

## Notes for PM
- Safe to archive? **yes, after optional BED cleanup ticket** — or archive now and defer F1/F2 as P2 backlog; neither blocks FED-20.31 Identity panel or QA Pass.
- If findings: prefer small BED-01 cleanup (F1 emotion snapshot reuse; F2 scorer merge) before calling the Host surface “done forever”; not required before user-facing Accept of PROP-20.30 behavior.
- Out of scope confirmed: bond / soak / step 7 routing; FED desktop formatter (PROP-20.31).
