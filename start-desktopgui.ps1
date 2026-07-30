#Requires -Version 5.1
<#
.SYNOPSIS
  Start House.ChatDesktop (Avalonia desktop GUI).
.DESCRIPTION
  Runs from repo root. Host is separate — start with SoulCore/scripts/start-soulcore.ps1
  if chat/WS is needed (defaults 127.0.0.1:7700).
.EXAMPLE
  .\start-desktopgui.ps1
  .\start-desktopgui.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$Project = Join-Path $RepoRoot "House\House.ChatDesktop\House.ChatDesktop.csproj"

if (-not (Test-Path -LiteralPath $Project)) {
    throw "ChatDesktop project not found: $Project"
}

Write-Host "Starting House.ChatDesktop ($Configuration)..."
Set-Location $RepoRoot
dotnet run --project $Project -c $Configuration
