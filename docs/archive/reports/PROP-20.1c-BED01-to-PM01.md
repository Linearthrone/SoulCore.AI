---
type: report
prop_id: PROP-20.1c
prop_root: PROP-20-victoria-mind-path
from: BED-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
title: Wire SixLabors license from env/gitignored file into MSBuild
upstream_fail: docs/agents/reports/PROP-20.1b-OPS01-to-PM01.md
---

# PROP-20.1c — ImageSharp Release license plumbing — Pass

## Done

1. **`Directory.Build.props`**: map `SIXLABORS_LICENSE_KEY` → `SixLaborsLicenseKey`; if no key, map gitignored `sixlabors.lic` at repo root or `SoulCore/` → `SixLaborsLicenseFile`. Native env `SixLaborsLicenseKey` already flows as an MSBuild property.
2. **`.gitignore`**: `sixlabors.lic`, `**/sixlabors.lic`, `*.lic`.
3. **Inference csproj comment**: corrected — ImageSharp **3.2+** (not only v4) hard-fails Release without a license.
4. **Runbook**: one note in `docs/runbooks/cursor-my-machines.md` (Release needs env/file; Debug warns only).
5. **No secret committed** — no real key invented/embedded; fake key used only for gate proof; cleared after probe.

## Acceptance

| Criterion | Result |
| --- | --- |
| Dummy/test key → past “no license found” | **Pass** — Release with `SIXLABORS_LICENSE_KEY=PROP-20.1c-FAKE-…` fails on deserialize/validate (`Kind` missing), not “No Six Labors license found” |
| Without key: Release fails honestly | **Pass** — “No Six Labors license found” |
| Debug still builds | **Pass** — Inference Debug succeeds with license warnings (Host Debug copy failed only due to running `.NET Host` file lock — unrelated) |
| No secret committed | **Pass** — `git check-ignore` matches `*.lic`; `git ls-files` has no `*.lic` |

## Files

- `Directory.Build.props` (license env/file wiring)
- `SoulCore/SoulCore.Inference/SoulCore.Inference.csproj` (comment fix)
- `.gitignore` (`*.lic` / `sixlabors.lic`)
- `docs/runbooks/cursor-my-machines.md` (brief Release license note)

## Evidence

### A) Release without license

```text
SixLaborsLicenseKey set: False
SIXLABORS_LICENSE_KEY set: False
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj -c Release --no-incremental

... ImageSharp.targets(35,5): error : No Six Labors license found. Set $(SixLaborsLicenseKey), set $(SixLaborsLicenseFile), or add a 'sixlabors.lic' file to the project/workspace.
Build FAILED.  0 Warning(s)  1 Error(s)
EXIT: 1
```

### B) Release with clearly fake `SIXLABORS_LICENSE_KEY` (proves MSBuild mapping)

Fake value used: `PROP-20.1c-FAKE-NOT-A-REAL-LICENSE-KEY` (not a real SixLabors key; cleared after probe).

```text
SIXLABORS_LICENSE_KEY set: True (value length=38)
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj -c Release --no-incremental

... ImageSharp.targets(35,5): error : Exception while validating Six Labors license: The given key 'Kind' was not present in the dictionary.
    at SixLabors.Licensing.LicenseSerializer.Deserialize(String value)
    at SixLabors.Licensing.ValidateLicenseTask.Execute()
Build FAILED.  0 Warning(s)  1 Error(s)
EXIT: 1
```

Gate change: **no longer** “No Six Labors license found” → env wiring works. A real key/file from OPS is still required for a green Release.

### C) Debug without license (Inference project — Host bin locked by PID 50312)

```text
dotnet build SoulCore/SoulCore.Inference/SoulCore.Inference.csproj -c Debug --no-incremental

... warning : No Six Labors license found. This Debug build can continue, but Release builds require a valid license.
SoulCore.Inference -> ...\bin\Debug\net8.0\SoulCore.Inference.dll
Build succeeded.  4 Warning(s)  0 Error(s)
EXIT: 0
```

### D) Gitignore / no tracked license

```text
git check-ignore -v sixlabors.lic SoulCore/sixlabors.lic
.gitignore:16:*.lic	sixlabors.lic
.gitignore:16:*.lic	SoulCore/sixlabors.lic

git ls-files "*.lic" "sixlabors.lic" "**/sixlabors.lic"
(empty — no lic tracked)
```

## OPS / PM next

1. Provision a **real** SixLabors license on home-pc (`SixLaborsLicenseKey` / `SIXLABORS_LICENSE_KEY` user/machine env, or gitignored `sixlabors.lic`).
2. Retry PROP-20.1b / PROP-20.3b Host **Release** build + deploy.

## Not done (out of scope)

- Obtaining / purchasing a real SixLabors key
- Downgrading ImageSharp
- Host restart / identity deploy (OPS)
