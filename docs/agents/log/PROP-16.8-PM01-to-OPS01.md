---
type: task
prop_id: PROP-16.8
prop_root: PROP-16-presence-glass-overlay
from: PM-01
to: OPS-01
priority: P0
status: Pass
report: docs/agents/log/PROP-16.8-OPS01-to-PM01.md
pm_note: Accepted 2026-10-23. Defaults are Host 7720, bridge 17920, Tailscale 7720/8453. Shared listeners were not stopped. Debug build failed only because pid 36096 already holds bin\Debug and is listening on 7720; Release build succeeded. ALLSTOP still targets 7700/17891/8000/8881 — that is 16.8b, not a reject of these defaults.
created: 2026-10-23
updated: 2026-10-23
depends_on: PROP-16.7 (Pass)
execution_repo: https://github.com/House-VictoriAI/SoulCore.AI_CreatorEdition
title: Creator Edition ports, off the Soul_Core listeners
---

# PROP-16.8 — Creator Edition port block

## Problem

Creator Edition Presence talks to `127.0.0.1:7700`. That process is not this checkout. It is `C:\Users\kurtw\Soul_Core\SoulCore\SoulCore.Host.dll`, SoulCore.Host 0.1.29, started 2026-10-21 23:10. `GET /api/personas` there is 404. `ALLSTART.ps1` treats any local SoulCore.Host as “already running” and leaves it. This tree’s Host is 0.2.0 and has the persona routes, and it never starts.

These listeners are already taken and must stay up:

| Port | Who |
| --- | --- |
| 7700 | Soul_Core Host (pid 7088) |
| 17891 | Browser bridge (pid 46640) |
| 8000 | STT (pid 22184) |
| 8881 | TTS (pid 54368) |
| 8443, 8444 | Tailscale serve already proxying the other stack |

## Lock

This checkout binds and dials the block below. One number, used in the Host, Presence, the bridge, the extension, and the start scripts.

| Surface | Port |
| --- | --- |
| Host HTTP and WebSocket | **7720** |
| ALLSTART alternate, if 7720 is taken | **7721** |
| Browser bridge and the Chrome extension | **17920** |
| Tailscale TCP forward | **7720** → `127.0.0.1:7720` |
| Tailscale HTTPS | **8453** → `127.0.0.1:7720` |

Leave these shared. This tree does not own the process:

- Ollama `11434`
- Unreal `8888`
- STT `8000` and TTS `8881` (the launchers in this repo cannot start a second copy; the Host keeps calling the listeners that are already up)

Do not stop anything on 7700, 17891, 8000, or 8881. Do not run `tailscale serve` off for 7700, 8443, or 8444. This script’s off switch removes only 7720 and 8453.

## Files

- `SoulCore/SoulCore.Config/HostBindOptions.cs`
- `SoulCore/SoulCore.Host/appsettings.json`, `appsettings.Development.json`, `appsettings.Example.json`, `Properties/launchSettings.json`
- `SoulCore/SoulCore.Config/VoiceOptions.cs` (`LastWavPublicUrl` only)
- `SoulCore/SoulCore.Config/ToolsOptions.cs` and `SoulCore/SoulCore.Inference/Tools/Browser/NativeBrowserBridge.cs`
- `House/House.ChatDesktop/Services/ConnectionDefaults.cs`, `LocalStackControl.cs`, and the persona-wizard sentence that names :7700
- `ALLSTART.ps1`, `SoulCore/scripts/start-soulcore.ps1`, `stop-soulcore.ps1`, `start-browser-bridge.ps1`, `tailscale-serve-soulcore.ps1`
- `BrowserCaptureBridge/bridge_server.py`
- `BrowserCaptureExtension/background.js`, `manifest.json`, `popup.js`
- Tests that assert these defaults. Do not edit the `+44 7700` phone fixture or the spend-meter `8000` token count.

`ALLSTART.ps1` default `-Port` is 7720. It passes that port to the Host, sets `HOUSE_SOULCORE_PORT`, and passes 17920 to the bridge and 7720/8453 to Tailscale serve.

## Do not

- Kill the running Host, bridge, STT, TTS, or the open Presence window.
- Commit or push.
- Rewrite handbook history. Update the tailscale runbook only where it would tell the operator to bind 7700 or 8443 for this tree.

## Acceptance

- [ ] A default start of this tree binds Host on 7720 and the bridge on 17920
- [ ] Presence defaults to 7720 without an env override
- [ ] `stop-soulcore.ps1` with no arguments targets 7720, not 7700
- [ ] Tailscale enable adds 7720 and 8453 only. Off removes those two only
- [ ] Nothing on 7700, 17891, 8000, or 8881 was stopped
- [ ] Report lists the commands and the listener check

## Report

`docs/agents/reports/PROP-16.8-OPS01-to-PM01.md`
