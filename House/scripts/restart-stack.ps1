#Requires -Version 5.1
<#
.SYNOPSIS
  PROP-16.2: Full local stack restart for Presence chrome "Restart stack".
.DESCRIPTION
  Runs ALLSTOP then ALLSTART from the repo root. Presence must spawn this
  detached (wait:false) because ALLSTOP kills House.ChatDesktop.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File House\scripts\restart-stack.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ScriptDir "..\..")).Path
$AllStop = Join-Path $RepoRoot "ALLSTOP.ps1"
$AllStart = Join-Path $RepoRoot "ALLSTART.ps1"
$LogDir = Join-Path $RepoRoot "House\artifacts"
$LogPath = Join-Path $LogDir "restart-stack.log"

if (-not (Test-Path -LiteralPath $AllStop)) { throw "Missing ALLSTOP.ps1 at $AllStop" }
if (-not (Test-Path -LiteralPath $AllStart)) { throw "Missing ALLSTART.ps1 at $AllStart" }

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-RestartLog {
    param([string]$Message)
    $line = "[{0:u}] {1}" -f (Get-Date).ToUniversalTime(), $Message
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}

Write-RestartLog "restart-stack begin repo=$RepoRoot"
Write-RestartLog "Running ALLSTOP.ps1…"
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $AllStop
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "ALLSTOP exited $LASTEXITCODE"
    }
    Write-RestartLog "ALLSTOP finished"
}
catch {
    Write-RestartLog "ALLSTOP failed: $_"
    throw
}

# Brief pause so ChatDesktop / Host PIDs finish exiting before ALLSTART.
Start-Sleep -Seconds 2

Write-RestartLog "Running ALLSTART.ps1…"
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $AllStart
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "ALLSTART exited $LASTEXITCODE"
    }
    Write-RestartLog "ALLSTART finished — restart-stack OK"
}
catch {
    Write-RestartLog "ALLSTART failed: $_"
    throw
}

exit 0
