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

# Load SOULCORE_* from SoulCore/.env so /ws companion auth works (same as start-soulcore).
# .env overwrites stale Process/User-inherited tokens — otherwise /health looks "up"
# while chat.send fails (WS Bearer mismatch).
$EnvFile = Join-Path $RepoRoot "SoulCore\.env"
$loadedCount = 0
if (Test-Path -LiteralPath $EnvFile) {
    foreach ($line in Get-Content -LiteralPath $EnvFile -Encoding utf8) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
        if ($trimmed.StartsWith("#")) { continue }
        $eq = $trimmed.IndexOf("=")
        if ($eq -lt 1) { continue }
        $key = $trimmed.Substring(0, $eq).Trim()
        if ($key -notlike "SOULCORE_*") { continue }
        $value = $trimmed.Substring($eq + 1).Trim()
        if (
            ($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))
        ) {
            if ($value.Length -ge 2) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }
        Set-Item -Path "Env:$key" -Value $value
        $loadedCount++
    }
    Write-Host "loaded/overwrote $loadedCount SOULCORE_* keys from .env for ChatDesktop"
}

Write-Host "Starting House.ChatDesktop ($Configuration)..."
Set-Location $RepoRoot
dotnet run --project $Project -c $Configuration
