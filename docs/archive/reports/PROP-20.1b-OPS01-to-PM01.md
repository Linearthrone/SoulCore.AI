---
type: report
prop_id: PROP-20.1b
prop_root: PROP-20-victoria-mind-path
from: OPS-01
to: PM-01
priority: P0
status: Fail
created: 2026-11-17
title: Host Release build blocked — SixLabors license (Identity deploy not applied)
depends_on: PROP-20.1
sibling_note: PROP-20.3b Host restart not combined — same Release gate blocks both
---

# PROP-20.1b — OPS Identity desk deploy

**Result: Fail**

Stopped at step 1 per ticket: Host **Release** must compile; do not patch product code (BED).

## Acceptance

| Criterion | Result |
| --- | --- |
| Host + Presence running on this wave’s build | **Fail** — Release build did not produce binaries; Host not restarted |
| `GET /settings/identity` 200 with anchors / identityAnchors | **Fail** — not probed (no healthy Host) |
| Report with command output | **Pass** — this file |

## Commands / evidence

### Preflight ports (before build)

```text
7700: Unable to connect to the remote server
7710: Unable to connect to the remote server
```

### Host Release build (required)

```powershell
dotnet build SoulCore/SoulCore.Host/SoulCore.Host.csproj -c Release
```

```text
Determining projects to restore...
Restored ... SoulCore.Protocol, Config, Inference, Adapters.Ws, Memory, Host
SoulCore.Protocol -> ...\bin\Release\net8.0\SoulCore.Protocol.dll
SoulCore.Core -> ...\bin\Release\net8.0\SoulCore.Core.dll
SoulCore.Config -> ...\bin\Release\net8.0\SoulCore.Config.dll
SoulCore.Memory -> ...\bin\Release\net8.0\SoulCore.Memory.dll
SoulCore.Adapters.Ws -> ...\bin\Release\net8.0\SoulCore.Adapters.Ws.dll
C:\Users\kurtw\.nuget\packages\sixlabors.imagesharp\3.2.0\build\SixLabors.ImageSharp.targets(35,5): error : No Six Labors license found. Set $(SixLaborsLicenseKey), set $(SixLaborsLicenseFile), or add a 'sixlabors.lic' file to the project/workspace. [C:\Users\kurtw\.house_victoria\SoulCore.AI\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj]
C:\Users\kurtw\.nuget\packages\sixlabors.imagesharp\3.2.0\build\SixLabors.ImageSharp.targets(35,5): error : Please obtain a license from https://sixlabors.com/pricing/ [C:\Users\kurtw\.house_victoria\SoulCore.AI\SoulCore\SoulCore.Inference\SoulCore.Inference.csproj]

Build FAILED.

    0 Warning(s)
    1 Error(s)

Time Elapsed 00:00:02.41
```

### Env / license presence (ops check only)

```text
SixLaborsLicenseKey set: False
SixLaborsLicenseFile: (empty)
sixlabors.lic in workspace: not found
```

### Debug build (informational — not used for deploy)

Debug compiles (ImageSharp emits license **warnings**, not errors). Ticket requires **Release**; ALLSTART default is Release. No Host restart / Presence rebuild / identity probe performed.

```text
Build succeeded.
... ImageSharp warning: No Six Labors license found. This Debug build can continue, but Release builds require a valid license.
    4 Warning(s)
    0 Error(s)
```

## Not done (blocked)

1. Restart Host (ALLSTART `-RestartHost`)
2. Rebuild/restart ChatDesktop Presence with PROP-20.1 Identity client
3. Probe `GET http://127.0.0.1:<port>/settings/identity`

## Blocker for BED / PM

`SixLabors.ImageSharp` **3.2.0** now hard-fails **Release** builds without `SixLaborsLicenseKey` / `SixLaborsLicenseFile` / `sixlabors.lic`. Comment in `SoulCore.Inference.csproj` assumed only ImageSharp 4 needed a paid key; current package targets disagree.

OPS did not change product code. Need either:

- License key/file provisioned for home-pc builds, or
- BED fix so Release builds without that gate (or pin/workaround that is product-owned)

## PROP-20.3b overlap

Same Release Host rebuild gate. No combined restart; migration-012 verification deferred until Release Host can start.
