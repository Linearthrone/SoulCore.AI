# PROP-4 OPS: install House Victoria Presence like a normal Windows app (shortcut + icon).
# Full Velopack auto-update is a follow-on — this gives Start Menu + Desktop today.
#
# Usage (from repo root, elevated optional):
#   powershell -NoProfile -ExecutionPolicy Bypass -File House/scripts/install-presence-shortcuts.ps1

$ErrorActionPreference = 'Stop'

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $repo 'House\House.ChatDesktop\House.ChatDesktop.csproj'))) {
  $repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
}

$exeCandidates = @(
  (Join-Path $repo 'House\House.ChatDesktop\bin\Release\net8.0\House.ChatDesktop.exe'),
  (Join-Path $repo 'House\House.ChatDesktop\bin\Debug\net8.0\House.ChatDesktop.exe')
)
$exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $exe) {
  Write-Host 'Building Release ChatDesktop…'
  dotnet build (Join-Path $repo 'House\House.ChatDesktop\House.ChatDesktop.csproj') -c Release
  $exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $exe) { throw 'House.ChatDesktop.exe not found after build' }

$ico = Join-Path $repo 'House\House.ChatDesktop\Assets\house-victoria.ico'
$wshell = New-Object -ComObject WScript.Shell

function New-Shortcut([string]$path, [string]$target) {
  $sc = $wshell.CreateShortcut($path)
  $sc.TargetPath = $target
  $sc.WorkingDirectory = Split-Path $target -Parent
  if (Test-Path $ico) { $sc.IconLocation = "$ico,0" }
  $sc.Description = 'House Victoria — Presence'
  $sc.Save()
  Write-Host "Wrote $path"
}

$startDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\House Victoria'
New-Item -ItemType Directory -Force -Path $startDir | Out-Null
New-Shortcut (Join-Path $startDir 'House Victoria Presence.lnk') $exe
New-Shortcut (Join-Path $env:USERPROFILE 'Desktop\House Victoria Presence.lnk') $exe

Write-Host "Done. Launch from Start Menu or Desktop. Exe: $exe"
Write-Host 'Next (OPS): Velopack/MSIX auto-update + toast — not in this script.'
