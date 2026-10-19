---
type: report
prop_id: PROP-19-her-screen-operator-clicks
from: PM-01
to: TT-01
environment: TINA-main
status: accepted-pm-ticketed
created: 2026-10-19
updated: 2026-10-19
---

# PROP-19 PM-01 accept (TINA-main)

**Route:** Avenue **C then B** (Kurt delivery / park+COM honesty → shared coord map → FED banner → QA). Accepted as TT recommended after Kurt Q1–Q2 locks.

**Operator brief:** Intervene on Her screen must work. Overlay is alive (badge tracks); fix Absolute delivery + aim, not “re-attach forever.” Confirm Presence ≥0.1.45 (#171 park) on desk during 19.1 — do **not** Pass the wave on park alone.

## PM locks

| Decision | Value |
| --- | --- |
| Kurt miss primary | Park/COM Absolute path (overlay alive per Q1) |
| Victoria offset primary | Shared slot→FB map; soft-cursor tip = inject point |
| Display | Windows default scale; Presence + Her screen on one monitor |
| Target surface | VM embed / `victoria-sandbox` COM Absolute |
| Product path | No OS `SetCursorPos` teleop; no `WS_EX_TRANSPARENT` on full-pane input |
| #171 | Avenue C partial only — still ticket 19.1–19.5 |

## Tickets issued

| Split | Role | Status | Notes |
| --- | --- | --- | --- |
| PROP-19.1 | QA-01 + BED-01 | Queued | Home PC matrix |
| PROP-19.2 | BED-01 | Queued | Park/COM delivery honesty |
| PROP-19.3 | BED-01 | Queued | Shared coord map |
| PROP-19.4 | FED-01 | Queued | Banner + paint on shared map |
| PROP-19.5 | QA-01 | Queued | Contested-focus + corner grid |

## Activation (user relays)

```text
@Agents/QA-01.md Start PROP-19.1.
@Agents/BED-01.md Start PROP-19.1.
```

After 19.1 findings land:

```text
@Agents/BED-01.md Start PROP-19.2.
@Agents/BED-01.md Start PROP-19.3.
```

After 19.3 map contract:

```text
@Agents/FED-01.md Start PROP-19.4.
@Agents/QA-01.md Start PROP-19.5.
```
