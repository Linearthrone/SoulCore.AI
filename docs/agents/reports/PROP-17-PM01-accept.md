---
type: report
prop_id: PROP-17-victoria-between-talks
from: PM-01
to: TT-01
environment: TINA-main
status: accepted-pm-ticketed
created: 2026-10-12
updated: 2026-10-12
---

# PROP-17 PM-01 accept (TINA-main)

**Route:** Avenue A (gate → procedure → R&R → shell). Accepted as recommended.

**Operator brief:** Diligent ticketing, hurried same-day execution. Victoria must park dead work and run new behaviors **today** for bug-out so she can **work tomorrow** without reconnect/hammer loops.

## PM locks

| Decision | Value |
| --- | --- |
| LeisureModel default | `qwen2.5:1.5b` (local `:11434` only; missing → quiet) |
| First park cooldown | 45 minutes, then one probe |
| Re-park cooldown | 120 minutes |
| Identical-failure threshold | 3 |
| Creativity clock | None |
| PROP-16 | Relocate to System in 17.4 — do not drop |

## Tickets issued

| Split | Role | Status | Notes |
| --- | --- | --- | --- |
| PROP-17.1 | BED-01 | **Partial** (park/rejects landed) | Avalonia Stop gap → 17.6 |
| PROP-17.2 | BED-01 | **Pass** | Tab/browser, SOP, life-hacks |
| PROP-17.3 | BED-01 | **Pass** (#150) | R&R LeisureModel |
| PROP-17.4 | FED-01 | **Pass** (#149 merged) | Three-tab shell |
| PROP-17.5 | FED-01 | **Pass** (#152) | Mood/doing-line; Android email |
| PROP-17.6 | FED-01 | **Pass** (#151) | Stop enabled while Starting Host |

## Activation (user relays)

```text
@Agents/BED-01.md Start PROP-17.1.
@Agents/BED-01.md Start PROP-17.2.
@Agents/FED-01.md Start PROP-17.4.
```

After 17.1 + 17.2 Pass: `@Agents/BED-01.md Start PROP-17.3.`  
After 17.4 email model: `@Agents/FED-01.md Start PROP-17.5.`
