---
type: task
prop_id: PROP-17.3
prop_root: PROP-17-victoria-between-talks
from: PM-01
to: BED-01
priority: P0
status: Pass
created: 2026-10-12
updated: 2026-10-12
wave: between-talks
title: Local LeisureModel R&R + self-started research vs suspended actions
depends_on: PROP-17.1 Partial accepted for park/rejects (Stop UI → 17.6); PROP-17.2 Pass
blocks: PROP-17.4 activity-band interest phrases (soft)
proposal: docs/agents/unexecuted_proposals/victoria-between-talks.md
intake: docs/agents/tasks/PROP-17-TT01-to-PM01.md
urgency: same-day once unblocked — she must exist between talks for bug-out
environment: TINA-main
---

# PROP-17.3 — Between-talks R&R (local leisure model)

**Activate now (PM unblocked 2026-10-12):** `@Agents/BED-01.md` then `Start PROP-17.3.`

Park + host rejects + tab/browser invariants are in (17.1 Partial / 17.2 Pass). Avalonia Stop-during-start is **17.6**, not a 17.3 gate.

## Operator urgency

After park and browser hygiene land, **ship R&R the same day** so Victoria keeps existing between talks while LinearThrone bug-hunts. She must be usable tomorrow — research thread alive, no creativity clock, no cloud/work-model fallback.

## PM locks (do not re-litigate)

| Knob | Value |
| --- | --- |
| `Inference:LeisureModel` default | `qwen2.5:1.5b` |
| Routing | Local `:11434` only (embedding-style local base URL; never reuse cloud `BaseUrl`) |
| Missing pull / VRAM contended | R&R **stays quiet** — no work-model substitute, no cloud |
| Creativity clock | **None** (20-minute stop overruled) |
| First park cooldown | 45m (owned by 17.1); R&R uses same park/probe for dead pages |

## Problem

When the assignment is dead, she either hammers it or goes quiet. There is no leisure lane. Chat/SMS/work share `presence-local` and continuation spam buries the human thread. Page text can poison the next preamble. She must not self-appoint buy/email/install/trade as the work assignment.

## Solution

1. Add `Inference:LeisureModel` + local-only routing seam (same pattern as `ToolModel` / `ToolModelUeLive`). Empty LeisureModel ≠ work model.
2. When nothing `in_progress` can run and no released (`todo`) task has a live surface → enter R&R. Suspended rows do **not** block R&R.
3. Exclusive inference lease + short keep-alive. Work runner does not tick during the lease.
4. **Self-started research** (neural interfaces, papers, notes, pictures, YouTube in guest browser): starts without a task and without release. Activity line: `Reading about X · local`. Notes → books/memory, not only the in-memory chat ring.
5. **Action proposals** (buy, email, install, trade, “drop the lesson and build it”): insert `suspended` for LinearThrone. Chat text must **not** call start-now on the work slot. Research continues.
6. Light tools only: look, read, write books, guest screenshots (Comfy only if lamp already up). Pages/YouTube in **guest** browser, not host Playwright.
7. Host rejects already in 17.1 apply. Page/screenshot/comment text stays inside the R&R turn — untrusted; short kept summary marked untrusted; cannot name tools, mint `confirmed`, or change settings; **not** copied into next work/chat preamble as instructions.
8. Human or SMS turn aborts in-flight R&R before next preamble.
9. Work/R&R continuation lines do **not** append to `presence-local`. One find worth reading = one message; band carries the rest.
10. Dead page during R&R → same park/probe as 17.1. Living research thread is **not** timer-killed.

## Do not

- Creativity clock / turn budget on interest
- Cloud or work-model fallback
- Auto-start VM to keep her busy
- Appoint research as the work assignment
- Presence shell (17.4) except providing status strings FED can bind

## Acceptance

- [x] LeisureModel loads only from local `:11434`; missing model → quiet R&R (test or config evidence)
- [x] R&R starts when no runnable released work; suspended does not block
- [x] Inference lease stops work runner ticks
- [x] Self-started research works without release; action proposals land `suspended`
- [x] Untrusted page text cannot poison next work preamble
- [x] Human/SMS aborts R&R; no continuation spam on `presence-local`
- [x] No wall-clock kill on a living research thread
- [x] Report: `docs/agents/reports/PROP-17.3-BED01-to-PM01.md`

## Handoff

Do not start until 17.1 and 17.2 report Pass. Same-day land after unblock.
