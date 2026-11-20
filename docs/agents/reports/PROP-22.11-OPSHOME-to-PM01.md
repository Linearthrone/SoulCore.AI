# PROP-22.11 — OPS-HOME → PM-01: Presence rebuild / Host restart steps

**Status:** Documented only. The live Host on `127.0.0.1:7710` and the running Presence were **not** stopped or restarted by this pass. (A second, isolated Presence build was launched for smoke with `LOCALAPPDATA` redirected to a temp folder and closed again.)

## Why a Host restart is needed

The Host currently on :7710 predates PROP-22.2. The new screens call `/operator/work-items`, `/operator/agenda`, `/settings/inference/profiles|apply`, `/settings/system-log`; against the old Host they show "Host does not have this route yet — restart Host." That text is expected until the Host is rebuilt.

## Rebuild + run Presence (this checkout)

```powershell
cd C:\Users\kurtw\.house_victoria\SoulCore.AI
dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj
dotnet test  House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj
dotnet run   --project House/House.ChatDesktop/House.ChatDesktop.csproj
```

Close the old Presence first (single VirtualBox HWND parent — two Presences fight over the embed).

## Restart the Host (only when you are ready)

```powershell
cd C:\Users\kurtw\.house_victoria\SoulCore.AI
powershell -NoProfile -ExecutionPolicy Bypass -File SoulCore/scripts/start-soulcore.ps1 -Port 7710 -RestartHost
curl.exe -s http://127.0.0.1:7710/health
```

Alternatively Presence → Settings → *SoulCore Host* → **Restart** (uses `LocalStackControl`). Migration 016 applies on first start.

## Installer / release (needs `gh` auth; version bump is OPS's call)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1 -Bump -Publish
```

I did **not** bump `House.ChatDesktop.csproj` (`1.1.0`) — `bump-versions.ps1` moves Host and Presence together; do it at release time and record it in `deploy_history.json` / `docs/版本变更记录.md` per OPS rules (nothing was deployed here, so no history rows were written).

## Settings-saved keys (SEC-01)

Ollama/Soniox/Synthetic keys saved in Presence land in `%LOCALAPPDATA%\HouseVictoria\secret-overrides.json` and in the Presence process env. A Host that Presence starts inherits them; an already-running Host needs the restart above. `SoulCore/.env` wins for non-empty keys.

## Post-restart verification (copy/paste)

```powershell
$t = (Get-Content SoulCore/.env | Select-String 'SOULCORE_COMPANION_API_TOKEN=(.*)').Matches.Groups[1].Value
$h = @{ Authorization = "Bearer $t" }
foreach ($p in '/operator/work-items','/operator/agenda','/settings/inference/profiles','/settings/system-log?lines=5') {
  "{0} -> {1}" -f $p, (Invoke-WebRequest "http://127.0.0.1:7710$p" -Headers $h -UseBasicParsing).StatusCode
}
```

Expect `200` for all four (do not paste the token anywhere).
