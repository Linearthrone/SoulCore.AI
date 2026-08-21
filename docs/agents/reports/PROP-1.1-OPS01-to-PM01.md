---
type: report
prop_id: PROP-1.1
legacy_task_id: TASK-201
from: OPS-01
to: PM-01
status: Pass
created: 2026-08-21
role: Operations
executed_by: Kurt (user kill-test)
pm_accept: WonderWoman (PM-01)
---

# PROP-1.1 OPS → PM-01 — Tablet SMS/MMS kill-test Pass

## Result

**Pass.** Gateway device is live on Kurt’s tailnet; SMS/MMS round-trip proven on the **tablet’s own talk/text MDN** (not DIGITS).

## PM product lock (2026-08-21)

| Decision | Lock |
| --- | --- |
| Victoria SMS identity | **Tablet cellular MDN** (SM-X218U talk/text) |
| DIGITS unused line | **Not required** for this ship — drop as gateway identity |
| Avenue | Still **B** (Android gateway → Tailscale → Host); only the E.164 source changed |

**Do not** commit the MDN, SIM ICCID, or Tailscale auth keys.

## Device

| Field | Value |
| --- | --- |
| Model | Samsung Galaxy Tab **SM-X218U** |
| Role | Spare always-on SMS/MMS gateway |
| Line | Device native talk/text (not DIGITS app line) |
| Tailscale | Installed; same tailnet as Host PC |
| Tailscale hostname | *(Kurt-held — put in private ops notes, not git)* |

## Kill-test matrix

| # | Test | Result |
| --- | --- | --- |
| A | Kurt daily phone → gateway MDN (SMS) | **Pass** (Kurt: done) |
| B | Gateway → Kurt daily (SMS) | **Pass** (Kurt: done) |
| C | Gateway → Kurt daily (MMS still / image) | **Pass** (Kurt: done) |

## Evidence notes

- Manual Messages UI on tablet; no Host adapter yet (expected for 1.1).
- Funnel / public `:7700` **not** used.

## Unblocks

- **PROP-1.2** BED inbound SMS/MMS HTTP → One Thread
- **PROP-1.3** BED outbound + screenshot MMS
- Then SEC **1.4** / QA **1.5** / FED Link shrink **1.6** after QA Pass

## Follow-ups for Kurt (ops hygiene)

1. Leave tablet **plugged in**, Wi‑Fi + Tailscale up (gateway must stay reachable).
2. Keep Kurt’s daily number for allowlist config (env only when BED ships).
3. Optional: note Tailscale MagicDNS hostname in a private password manager — not in repo.
