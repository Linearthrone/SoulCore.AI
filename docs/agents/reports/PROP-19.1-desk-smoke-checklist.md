---
type: checklist
prop_id: PROP-19.1
from: PM-01 / TINA
to: Kurt (Home PC)
created: 2026-10-19
updated: 2026-10-19
---

# PROP-19.1 — Desk smoke (run when Presence build is ready)

**Need:** Presence **≥0.1.46** (or whatever version the execute PR bumps to — check About / `InformationalVersion`). Confirm #171 park land is included.

## Before clicks

1. Presence maximized on **one** monitor (your usual layout).
2. Her screen shows embedded `victoria-sandbox`.
3. Move pointer over Her screen — **badge / soft-cursor sibling tracks?** (expect yes)

## Kurt intervene (3 tries)

On a known guest control (e.g. desktop icon / Firefox URL bar):

| # | Badge tracks? | Guest reacted? | Notes |
| --- | --- | --- | --- |
| 1 | | | |
| 2 | | | |
| 3 | | | |

## Log lines (paste from `presence-startup.log` or equivalent)

Look for `her-screen click` lines with `park=ok` or `park=failed` and `sent` / `COM error`:

```
(paste 2–3 lines)
```

## Victoria aim (one tool click)

Ask her to click something visible. Soft-cursor tip vs actual hit — rough px miss at edges if obvious:

```
(note)
```

## Optional Q4

After Recover / re-embed: clicks work briefly then die? Y/N/unsure: ____

## Hand back

Relay results to TINA / paste into `PROP-19.1-QA01-to-PM01.md` when ready.
