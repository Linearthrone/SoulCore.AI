---
type: pm-accept
prop_id: PROP-6.1
from: PM-01
to: BED-01
status: Accepted
created: 2026-09-05
branch: cursor/prop6-desktop-delay-8a1f
verdict: Pass
---

# PM Accept — PROP-6.1

**Accepted Pass.** `Thread.Sleep` removed from `NativeDesktopControlBackend`; cancellable `Task.Delay`; tests 148 desktop / 5 new. Fence held (no Host/Memory).

Ready to merge independently of PROP-5.
