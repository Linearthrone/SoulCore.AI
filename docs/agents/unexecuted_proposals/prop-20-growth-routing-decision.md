---
type: decision
prop_id: PROP-20-growth-routing
from: LinearThrone
via: PM-01
status: locked
created: 2026-11-18
updated: 2026-11-18
closes: step-7-growth-routing
---

# PROP-20 — Growth routing (step 7)

## Locked

### Inside-band vs band-cross

- **Inside a band** (Low / Mid / High): engine deltas **auto-apply** with a ledger row. Free to move as designed.
- **Band upgrade or downgrade**: stays **pending** until operator confirms. Only class that waits for Kurt.

Band edges (plan): Low 0.15–0.30, Mid 0.45–0.60, High 0.80–0.95.

### Soak

- **14 days display-only** for bond counters (wall-clock from soak clock start / first Identity display of counters).
- After soak opens: counters may nudge **only** Compass growth traits **responsiveness**, **initiative**, **openness**.
- First week after soak opens: only **small inside-band** nudges on those three.
- Band-cross on those traits still waits for operator (same rule as above).
- **Minimum contact gate:** soak does **not** open if fewer than **3 days with contact** in the 14-day window (quiet fortnight must not fake intimacy).

## Still out of scope here

PROP-18 Q1/Q3/Q4, SMS login fill, step 8 second wave.
