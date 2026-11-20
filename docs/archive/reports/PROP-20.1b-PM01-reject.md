---
type: report
prop_id: PROP-20.1b
from: PM-01
to: PM-01
status: Rejected
created: 2026-11-17
related_report: docs/agents/reports/PROP-20.1b-OPS01-to-PM01.md
blocker: SixLabors.ImageSharp 3.2.0 Release requires license; machine has none
---

# PROP-20.1b — PM reject (ops gate)

OPS Fail accepted as accurate. Release Host cannot build without `SixLaborsLicenseKey` / file / `sixlabors.lic`. Same gate blocks PROP-20.3b Release path.

**Immediate unblock (Wave A smoke):** re-hand OPS with **Debug** ALLSTART authorized.
**Release/production unblock:** BED wires env→MSBuild license prop (no secret in git) + operator supplies key; then OPS Retry Release.
