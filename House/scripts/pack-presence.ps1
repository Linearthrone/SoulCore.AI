# PROP-4.2 - pack House Victoria Presence as a Velopack Windows installer.
#
# Prerequisites (Windows):
#   dotnet tool install -g vpk
#   (same major as Velopack package in House.ChatDesktop.csproj)
#
# Usage (repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/pack-presence.ps1 -Version 0.1.1
#
# Output:
#   House/artifacts/presence-publish/     published app
#   House/artifacts/presence-releases/  Setup.exe + nupkg + releases.*.json
#
# Install: run Setup.exe from presence-releases (Start Menu shortcut created by Velopack).
# Updates: Presence checks GitHub Releases (Linearthrone/SoulCore.AI) or HOUSE_VICTORIA_UPDATE_URL.
# Upload the contents of presence-releases/ to a GitHub Release (or your HTTP feed).

param(
  [string]$Version = "",
  [string]$Channel = "win"
)

$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$csproj = Join-Path $repo 'House\House.ChatDesktop\House.ChatDesktop.csproj'
if (-not (Test-Path $csproj)) { throw "Missing $csproj" }

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
Write-Host 'Run Setup.exe on the target PC. Then Presence Settings > Updates (or title Update) to check for newer releases.'
Write-Host 'Publish tip: attach the release files to a GitHub Release so GithubSource can find them.'
