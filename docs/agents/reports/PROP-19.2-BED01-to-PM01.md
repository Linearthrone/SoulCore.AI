---
type: report
prop_id: PROP-19.2
prop_root: PROP-19-her-screen-operator-clicks
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Park/COM Absolute delivery honesty
environment: TINA-main
branch: cursor/prop19-execute-6372
commit: 36f7d60
---

# PROP-19.2 — Park/COM delivery honesty

## Verdict

**Pass** (code + unit tests). Desk Absolute land remains **19.1 / 19.5**.

## Landed

- Do **not** Absolute-inject when park fails; restore host cursor and throw → `ReportFault`.
- Log lines never claim `sent` when `park=failed` (`IsHonestClickSuccess` / `FormatClickDeliveryLine`).
- Pre-inject failures (`no mouse`, unmapped) log via `FormatClickFailedBeforeInjectLine` / dropped-unmapped + ReportFault.
- Operator-facing faults via `FormatOperatorClickFault` (FED 19.4 wires banner / `FaultCleared`).
- Embed-only park exclude retained (#171); 19.3 `MapSlotToInject` path re-asserted on inject STA thread.

## Commits

| SHA | Summary |
| --- | --- |
| `b179ce9` | Park/COM honesty — skip inject on park fail; honest log lines |
| `476d822` | Restore `MapSlotToInject`; `FormatOperatorClickFault` |
| `36f7d60` | Unit test for operator fault text |

## Tests

`dotnet test --filter VictoriaVmPointerPlanTests` → **44 passed** (0 failed).

## Presence version

Branch already at **0.1.46** (19.3). Working tree may show **0.1.47** from parallel 19.4 — leave bump to that agent/coordinator.

## Desk (19.5)

Expect startup log `her-screen click … park=ok … sent` or honest `park=failed` / banner — never silent miss. No OS `SetCursorPos` teleop; no `WS_EX_TRANSPARENT` on full-pane input.

## Gaps

- **19.3:** Shared map already on branch; soft-cursor tip = inject Guest0.
- **19.4:** Banner polish / overlay-missing surface (WIP on branch).
- **19.5:** Contested-focus + corner grid desk smoke with Kurt Absolute.
