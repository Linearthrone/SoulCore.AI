# ALLSTART desk stack

## What it starts

On the Windows home PC, `ALLSTART.ps1` typically brings up:

1. SoulCore Host (`:7700`)
2. Tailscale serve (optional / scripted)
3. BrowserCaptureBridge (native browser helper)
4. House.ChatDesktop
5. House.Voice STT/TTS (unless `-SkipVoice`)

Stop with `ALLSTOP.ps1`.

## Common flags

See script header comments in `ALLSTART.ps1` for `-RestartHost`, `-SkipVoice`, etc.

## Hermes

Hermes is **not** started. References to `-WithHermes` in old docs are stale.

## Verify

```powershell
curl.exe -sS http://127.0.0.1:7700/health
.\SoulCore\scripts\ws-companion-auth-probe.ps1
```
