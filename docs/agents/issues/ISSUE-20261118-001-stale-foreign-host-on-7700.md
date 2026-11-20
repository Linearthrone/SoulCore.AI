---
issue_id: ISSUE-20261118-001
discovered: 2026-11-18
severity: P2 (Major)
status: Pending Fix
prop_related: PROP-20.37b, PROP-20.39
---

## Problem Description

Two SoulCore.Host processes are listening on the machine. The House Victoria / ALLSTART Host on **:7710** (v0.1.54) correctly exposes PROP-20.37 Identity growth fields. A **foreign** Host on **:7700** (v0.1.52) from another tree does not.

## Reproduction Steps

1. `Get-NetTCPConnection -LocalPort 7700,7710 -State Listen`
2. Inspect process command lines.
3. `GET http://127.0.0.1:7700/settings/identity` vs `GET http://127.0.0.1:7710/settings/identity`

## Expected Result

Single House Victoria Host (ALLSTART default :7710) without a competing foreign binary on :7700.

## Actual Result

| Port | PID | Binary | Version | Growth Identity fields |
| --- | --- | --- | --- | --- |
| 7710 | 31064 | `c:\Users\kurtw\.house_victoria\SoulCore.AI\SoulCore\SoulCore.Host\bin\Release\net8.0\SoulCore.Host.dll` | 0.1.54 | Present |
| 7700 | 54648 | `C:\Users\kurtw\Soul_Core\SoulCore\SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll` | 0.1.52 | Missing |

`:7700` Identity keys only: `displayName,contactId,charter,identityAnchors,anchors,note`. Confirm/dismiss growth routes return empty 404 (route not registered).

## Impact Scope

Any client still pointed at :7700 (older docs / Android reverse / Unreal README) will miss bond/soak/growth Identity JSON. ChatDesktop default is :7710, so primary desk path is OK.

## Fix suggestion

OPS-01 (PROP-20.37b): stop PID 54648 foreign Host; keep one SoulCore.AI Host; re-check `GET /settings/identity` has `bondCounters`/`soak`.
