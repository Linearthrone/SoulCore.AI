---
type: task
prop_id: PROP-20.11
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-17
title: Wave A slop cleanup F1–F7 + F9 (keep F8 façade)
depends_on: PROP-20.10
slop_report: docs/agents/reports/PROP-20.10-SLOP01-to-PM01.md
pm_disposition: docs/agents/reports/PROP-20.10-PM01-accept.md
---

# PROP-20.11 — BED slop cleanup

**Activate:** `@Agents/BED-01.md`

## Do (cite F-IDs in report)

1. **F1** Remove dead `SmsTools` alias from `SmsToolGate.cs`.
2. **F2** One truncate helper on `SmsOutboundComposer`; inbound/outbound call it; drop twins; break Outbound→Inbound truncate dependency.
3. **F3** Dedupe `MemoryTape.StripUntrusted` vs `UntrustedObservation.StripFromPreamble`.
4. **F4** Single name for leisure write-as-her gate (`MayWriteAsHer` *or* `ContainsRequiredMind` — pick one).
5. **F5** Remove `LeisureMindBlock.SharedBuilder` + silent catch; fix tests to use `VictoriaMindBlock` / DI.
6. **F6** Drop leisure constant re-exports / `BuildTemporaryMidDefaults` if unused after test fix; use `VictoriaMindBlock` markers.
7. **F7** Remove legacy bare `[Resting]` accept branch.
8. **F9** Fix `IVictoriaMindBlockBuilder` XML — no “related memories” claim.
9. **F8** Do **not** remove `ChatContextBuilder.BuildMemoryBlock` façades.

## Verify

Targeted `dotnet test` for SMS, leisure, ChatContextBuilder, mind block suites — paste output.

## Report

`docs/agents/reports/PROP-20.11-BED01-to-PM01.md`
