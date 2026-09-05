---
type: task
prop_id: PROP-6.1
prop_root: PROP-6-desktop-drag-async-delay
from: PM-01
to: BED-01
priority: P1
status: Pending
created: 2026-09-05
wave: wipeout-now
title: NativeDesktopControlBackend — Thread.Sleep → Task.Delay
depends_on: none
parallel_with: PROP-5.*
proposal: docs/agents/unexecuted_proposals/desktop-drag-async-delay.md
intake: docs/agents/tasks/PROP-6-TT01-to-PM01.md
report: docs/agents/reports/PROP-6.1-BED01-to-PM01.md
---

# PROP-6.1 — Desktop drag async delay

## Problem

`NativeDesktopControlBackend` drag interpolation uses `Thread.Sleep(15)` inside Task-returning methods (~300ms thread pin per drag).

## Solution

1. Replace all `Thread.Sleep` on this backend with `await Task.Delay(…, ct)`.
2. Honor cancellation.
3. Extend/add test that drag path does not block synchronously for the full interpolate window.
4. **Fence:** `SoulCore.Inference/Tools/Desktop/` (+ tests) only — **no** Host / Memory / Hermes edits.

## Acceptance

| # | Criterion |
| --- | --- |
| 1 | Zero `Thread.Sleep` in `NativeDesktopControlBackend.cs` |
| 2 | Delays cancellable |
| 3 | Desktop tool tests pass |
| 4 | Report shows before/after + test output |

## Parallel

Safe beside PROP-5 (different project). Separate PR from Host work.
