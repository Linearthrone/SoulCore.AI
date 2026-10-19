---
type: report
prop_id: PROP-19.2
prop_root: PROP-19-her-screen-operator-clicks
from: BED-01
to: PM-01
priority: P0
status: Partial
created: 2026-10-19
updated: 2026-10-19
wave: her-screen-clicks
title: Park/COM Absolute delivery honesty
environment: TINA-main
commit: b179ce9 (+ follow-up MapSlotToInject restore)
---

# PROP-19.2 — BED-01 → PM-01

**Status: Partial** — delivery honesty hardened in code + unit tests; Kurt desk Absolute land remains for 19.5.

## What changed

| Area | Change |
| --- | --- |
| `VictoriaVmPointerPlan.FormatClickDeliveryLine` | Never appends `sent` when `park=failed` (even if caller passes `sent:true`). Adds `IsHonestClickSuccess`, `ParkFailedReason`, `FormatClickFailedBeforeInjectLine`. |
| `VictoriaVmPointer.InjectPointer` | Park fail → **no Absolute inject**, log `park=failed COM error: park failed`, throw → `ReportFault` (Kurt action text). COM fail after park → `park=ok` + COM error (no `sent`). |
| `WithCursorParked` | Sets `parkOk` before body; skips body on park fail; no longer injects when `GetCursorPos` fails. |
| Deliver paths | Console-missing / unmapped surface now log delivery lines + ReportFault (no silent drop). |
| Tests | Park/sent honesty + embed-only park policy lines. |

**Presence version:** left at **0.1.46** (already bumped on this branch by PROP-19.3). No further bump from 19.2.

## Evidence

- Commit: `b179ce9` on `cursor/prop19-execute-6372` (plus follow-up restore of 19.3 `MapSlotToInject` if needed).
- Unit tests: `VictoriaVmPointerPlanTests` — **43 passed** (`dotnet test --filter VictoriaVmPointerPlanTests`).
- Embed-only exclude policy unchanged (`BuildParkExcludes` = embed HWND only, not GA_ROOT) — covered by existing + new park policy tests.

## Acceptance checklist

- [x] Every inject attempt logs park + sent/COM outcome (FormatClickDeliveryLine / failed-before-inject / dropped-unmapped).
- [x] No silent “success” when park or COM fails (`park=failed sent` eliminated; inject skipped on park fail).
- [ ] Kurt center click lands on guest control — **needs desk QA (19.5)** with Presence ≥0.1.45/0.1.46; not verified in this cloud agent environment.

## Gaps for 19.3 / 19.5

- **19.3:** Shared slot→Guest0 map + soft-cursor tip already on branch (`15254c6`). Inject path must keep `MapSlotToInject` (re-asserted if 19.2 edit briefly touched it).
- **19.4:** FED banner for delivery faults (Faulted already surfaces to `VictoriaBrowserActionText`; banner polish is FED).
- **19.5:** Contested-focus + corner grid desk smoke; confirm `her-screen click … park=ok sent` on Kurt Absolute with MI active.

## Do-not (confirmed)

- No OS `SetCursorPos` as product click path (park-only host cursor move).
- No `WS_EX_TRANSPARENT` on full-pane input.
- Soft-cursor paint not rewritten.
