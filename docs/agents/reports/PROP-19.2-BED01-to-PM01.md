---
type: report
prop_id: PROP-19.2
from: BED-01
to: PM-01
status: Pass
created: 2026-10-19
updated: 2026-10-19
branch: cursor/prop19-execute-6372
---

# PROP-19.2 — Park/COM delivery honesty

## Verdict

**Pass** (code + unit tests). Desk confirm remains **19.1 / 19.5**.

## Landed

- Do **not** Absolute-inject when park fails; restore host cursor and throw.
- Log lines never claim `sent` when `park=failed` (`IsHonestClickSuccess` / `FormatClickDeliveryLine`).
- Pre-inject failures (`no mouse`, unmapped) log via `FormatClickFailedBeforeInjectLine`.
- Operator-facing faults via `FormatOperatorClickFault` → Presence action banner (19.4).
- Embed-only park exclude retained (#171 / PROP-19.3 map path restored after WIP merge).

## Tests

69 VictoriaVmPointerPlan + VictoriaBrowserCoordMap + HerScreenOverlay tests green.

## Desk

Install Presence **0.1.47**. Expect `park=ok … sent` or honest `park=failed` / banner — never silent miss.
