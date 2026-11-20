---
type: report
prop_id: PROP-20.10
from: PM-01
status: Accepted-findings
created: 2026-11-17
related_report: docs/agents/reports/PROP-20.10-SLOP01-to-PM01.md
---

# PROP-20.10 — PM disposition

SLOP findings Accepted. Wave A **not** archived until P1 cleanup Pass.

| ID | Disposition |
| --- | --- |
| F1 | **remove** `SmsTools` |
| F2 | **dedupe** truncate → `SmsOutboundComposer` |
| F3 | **dedupe** strip → `UntrustedObservation` |
| F4 | **dedupe** one write-as-her name |
| F5 | **remove** `SharedBuilder` hook (DI/`VictoriaMindBlock` enough) |
| F6 | **dedupe** — use Core markers; drop test-only temporary builder |
| F7 | **remove** bare `[Resting]` branch (no Wave A emitter) |
| F8 | **keep** `ChatContextBuilder.BuildMemoryBlock` façade (API stability) |
| F9 | **remove** “related memories” from builder XML |

Execution: **PROP-20.11** → BED-01.
