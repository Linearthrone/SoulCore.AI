# PROP-4.2 - shortcut helper for *dev* builds (bin/Release).
# Prefer the Velopack Setup.exe from pack-presence.ps1 for a real install + updates.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/install-presence-shortcuts.ps1

$ErrorActionPreference = 'Stop'

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $repo 'House\House.ChatDesktop\House.ChatDesktop.csproj'))) {
  $repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
}

$exeCandidates = @(
  (Join-Path $repo 'House\House.ChatDesktop\bin\Release\net8.0\win-x64\House.ChatDesktop.exe'),
  (Join-Path $repo 'House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe'),
  (Join-Path $repo 'House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.exe'),
  (Join-Path $repo 'House\artifacts\presence-publish\House.ChatDesktop.exe')
)
$exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) {
  Write-Host 'Building Release ChatDesktop...'
  dotnet build (Join-Path $repo 'House\House.ChatDesktop\House.ChatDesktop.csproj') -c Release
  $exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $exe) { throw 'House.ChatDesktop.exe not found after build' }

# Taskbar / Start Menu / exe use the SC mark (16, 32, 48 inside soulcore.ico).
# The desktop shortcut uses the Soul Core wordmark.
$appIco = Join-Path $repo 'House\House.ChatDesktop\Assets\soulcore.ico'
$desktopIco = Join-Path $repo 'House\House.ChatDesktop\Assets\Desktop_Icon_SoulCore_AI.ico'
$wshell = New-Object -ComObject WScript.Shell

function New-Shortcut([string]$path, [string]$target, [string]$icon) {
  $sc = $wshell.CreateShortcut($path)
  $sc.TargetPath = $target
  $sc.WorkingDirectory = Split-Path $target -Parent
  if ($icon -and (Test-Path $icon)) { $sc.IconLocation = "$icon,0" }
  $sc.Description = 'House Victoria - Presence'
  $sc.Save()
  Write-Host "Wrote $path"
}

$startDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\House Victoria'
New-Item -ItemType Directory -Force -Path $startDir | Out-Null
New-Shortcut (Join-Path $startDir 'House Victoria Presence.lnk') $exe $appIco
New-Shortcut (Join-Path $env:USERPROFILE 'Desktop\House Victoria Presence.lnk') $exe $desktopIco

Write-Host "Done. Launch from Start Menu or Desktop. Exe: $exe"
Write-Host 'For a real installer + Update button feed: House/scripts/pack-presence.ps1 -> run Setup.exe.'
