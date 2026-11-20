---
type: task
prop_id: PROP-20.1c
prop_root: PROP-20-victoria-mind-path
from: PM-01
to: BED-01
priority: P0
status: Pending
created: 2026-11-17
title: Wire SixLabors license from env for Release builds (no secret in git)
depends_on: none
blocks: PROP-20.1b Release retry, PROP-20.3b Release
upstream_fail: docs/agents/reports/PROP-20.1b-OPS01-to-PM01.md
---

# PROP-20.1c — ImageSharp Release license plumbing

**Activate:** `@Agents/BED-01.md`

## Problem

`SixLabors.ImageSharp` 3.2.0 hard-fails **Release** without a license. `SoulCore.Inference.csproj` comment assumed only v4 needed a key — wrong. Machine has no key/file. Production path (ALLSTART default Release) is blocked for all Wave A Host deploys.

## Solution

1. In `Directory.Build.props` (or Inference csproj), map environment / user-local file into `SixLaborsLicenseKey` or `SixLaborsLicenseFile` **without committing secrets**.
   - Prefer: if env `SIXLABORS_LICENSE_KEY` or `SixLaborsLicenseKey` is set → MSBuild property.
   - Optional: if `SoulCore/sixlabors.lic` or repo-root `sixlabors.lic` exists (gitignored) → `SixLaborsLicenseFile`.
2. Ensure `sixlabors.lic` / `*.lic` are gitignored if not already.
3. Fix the outdated comment on the ImageSharp PackageReference.
4. Document one line in `docs/runbooks/cursor-my-machines.md` (or Host runbook): Release needs this env/file; Debug warns only.
5. Do **not** invent or embed a license key. Do **not** downgrade ImageSharp solely to dodge licensing unless PM reopens that avenue.

## Acceptance

- [ ] With a dummy/test key set in env for a local probe, Release build gets past the ImageSharp license target (or clearly fails only on invalid key after the “no license found” path is fixed)
- [ ] Without key: still fails Release honestly; Debug still builds
- [ ] No secret committed
- [ ] Report: `docs/agents/reports/PROP-20.1c-BED01-to-PM01.md`
