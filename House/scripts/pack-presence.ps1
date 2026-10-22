# PROP-4.2 - pack House Victoria Presence as a Velopack Windows installer.
#
# Prerequisites (Windows):
#   dotnet tool install -g vpk
#   (same major as Velopack package in House.ChatDesktop.csproj)
#   For -Publish: GitHub CLI `gh` authenticated with repo release rights
#
# Usage (repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1 -Bump
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1 -Bump -Publish
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1 -Version 0.1.2 -Publish
#
# Output:
#   House/artifacts/presence-publish/     published app
#   House/artifacts/presence-releases/  Setup.exe + nupkg + releases.*.json
#
# Install: run Setup.exe from presence-releases (Start Menu shortcut created by Velopack).
# Presence auto-starts SoulCore.Host + Ollama on open when Auto-start is enabled (default).
# Point Settings > System > SoulCore repo folder at your checkout (folder with ALLSTART.ps1),
# or set HOUSE_SOULCORE_REPO - required when the app is installed outside the repo.
# Updates: Presence checks GitHub Releases (House-VictoriAI/SoulCore.AI_CreatorEdition) or HOUSE_VICTORIA_UPDATE_URL.
# -Publish uploads presence-releases/* to a GitHub Release tag presence-v{version} so Update works.
# Prefer -Bump (or House/scripts/bump-versions.ps1) before packing so every fix gets a new version.
#
# IMPORTANT: Keep this file ASCII-only. Windows PowerShell 5.x defaults to a non-UTF8
# code page and will break on em-dashes / arrows inside double-quoted strings
# (that is why Presence Release CI never published Setup.exe).

param(
  [string]$Version = "",
  [string]$Channel = "win",
  [switch]$Bump,
  [switch]$Publish
)

$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$csproj = Join-Path $repo 'House\House.ChatDesktop\House.ChatDesktop.csproj'
if (-not (Test-Path $csproj)) { throw "Missing $csproj" }

if ($Bump) {
  if ($Version) { throw 'Use either -Bump or -Version, not both.' }
  & (Join-Path $PSScriptRoot 'bump-versions.ps1') -Target presence -Part patch
  if ($LASTEXITCODE -ne 0) { throw 'bump-versions.ps1 failed' }
}

if (-not $Version) {
  [xml]$xml = Get-Content $csproj
  $Version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
  if (-not $Version) { $Version = '0.1.0' }
}

$publishDir = Join-Path $repo 'House\artifacts\presence-publish'
$releaseDir = Join-Path $repo 'House\artifacts\presence-releases'
$ico = Join-Path $repo 'House\House.ChatDesktop\Assets\house-victoria.ico'

Write-Host "Publishing Presence $Version ..."
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir /p:Version=$Version /p:InformationalVersion=$Version
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
  Write-Host 'Installing vpk global tool...'
  dotnet tool install -g vpk
  $vpk = Get-Command vpk -ErrorAction SilentlyContinue
}
if (-not $vpk) { throw 'vpk not found after install. Open a new shell or add %USERPROFILE%\.dotnet\tools to PATH.' }

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$packArgs = @(
  'pack',
  '--packId', 'HouseVictoria.Presence',
  '--packVersion', $Version,
  '--packDir', $publishDir,
  '--mainExe', 'House.ChatDesktop.exe',
  '--outputDir', $releaseDir,
  '--channel', $Channel
)
if (Test-Path $ico) {
  $packArgs += @('--icon', $ico)
}

Write-Host "vpk $($packArgs -join ' ')"
& vpk @packArgs
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed' }

Write-Host ''
Write-Host "Done. Installer folder: $releaseDir"
Write-Host 'Installer file: HouseVictoria.Presence-win-Setup.exe (Velopack renames Setup.exe).'
Write-Host 'IMPORTANT: Setup.exe is the INSTALLER - it installs, launches Presence once, then exits.'
Write-Host 'If nothing appears, open Start Menu -> HouseVictoria.Presence (or House Victoria Presence),'
Write-Host 'and check %LocalAppData%\HouseVictoria\presence-startup.log for a crash trail.'
Write-Host 'Then Presence Settings > Updates (or title Update) to check for newer releases.'

if ($Publish) {
  $gh = Get-Command gh -ErrorAction SilentlyContinue
  if (-not $gh) { throw 'gh CLI not found - install GitHub CLI to use -Publish.' }

  $tag = "presence-v$Version"
  $assets = @(Get-ChildItem -Path $releaseDir -File | ForEach-Object { $_.FullName })
  if ($assets.Count -eq 0) { throw "No files in $releaseDir to publish." }

  & gh release view $tag 2>$null | Out-Null
  if ($LASTEXITCODE -eq 0) {
    Write-Host "Release $tag already exists - uploading/replacing assets..."
    & gh release upload $tag @assets --clobber
    if ($LASTEXITCODE -ne 0) { throw "gh release upload failed for $tag" }
  }
  else {
    Write-Host "Creating GitHub Release $tag ..."
    & gh release create $tag @assets `
      --title "Presence $Version" `
      --notes "House Victoria Presence $Version (Velopack). Install Setup.exe, or use Update in an existing install. Host is separate - restart Host for Playwright/tool fixes."
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed for $tag" }
  }

  Write-Host "Published $tag - installed Presence can now use Update."
}
else {
  Write-Host 'To feed the Update button: re-run with -Publish (or let the Presence Release GitHub Action pack on main).'
}
