#Requires -Version 5.1
<#
.SYNOPSIS
  PROP-16.2: Full local stack restart for Presence chrome "Restart stack".
.DESCRIPTION
  Runs ALLSTOP (wait), then starts ALLSTART detached. Presence must spawn this
  script detached (wait:false) because ALLSTOP kills House.ChatDesktop.

  ALLSTART ends with start-desktopgui.ps1 (dotnet run) and does not exit while
  Presence is open -- so this script must NOT wait on ALLSTART.

  IMPORTANT: Keep this file ASCII-only. Windows PowerShell 5.x defaults to a
  non-UTF8 code page and will parse-fail on ellipsis / em-dash / arrows
  (same rule as pack-presence.ps1).
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File House\scripts\restart-stack.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# House/scripts -> repo root (two levels up), same as pack-presence.ps1.
$RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$AllStop = Join-Path $RepoRoot 'ALLSTOP.ps1'
$AllStart = Join-Path $RepoRoot 'ALLSTART.ps1'
$LogDir = Join-Path $RepoRoot 'House\artifacts'
$LogPath = Join-Path $LogDir 'restart-stack.log'
$AllStartOut = Join-Path $LogDir 'restart-stack-allstart.out.log'
$AllStartErr = Join-Path $LogDir 'restart-stack-allstart.err.log'

if (-not (Test-Path -LiteralPath $AllStop)) { throw "Missing ALLSTOP.ps1 at $AllStop" }
if (-not (Test-Path -LiteralPath $AllStart)) { throw "Missing ALLSTART.ps1 at $AllStart" }

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
Set-Location -LiteralPath $RepoRoot

function Write-RestartLog {
    param([Parameter(Mandatory = $true)][string]$Message)
    $stamp = Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ'
    $line = "[$stamp] $Message"
    Add-Content -LiteralPath $LogPath -Value $line -Encoding utf8
    Write-Host $line
}

$powershellExe = (Get-Command powershell.exe -ErrorAction Stop).Source

Write-RestartLog "restart-stack begin repo=$RepoRoot"
Write-RestartLog "Running ALLSTOP.ps1..."
try {
    $stop = Start-Process -FilePath $powershellExe `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $AllStop) `
        -WorkingDirectory $RepoRoot `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    if ($null -eq $stop) {
        throw 'failed to start ALLSTOP process'
    }
    if ($stop.ExitCode -ne 0) {
        throw "ALLSTOP exited $($stop.ExitCode)"
    }
    Write-RestartLog 'ALLSTOP finished'
}
catch {
    Write-RestartLog "ALLSTOP failed: $($_.Exception.Message)"
    throw
}

# Brief pause so ChatDesktop / Host PIDs finish exiting before ALLSTART.
Start-Sleep -Seconds 2

Write-RestartLog "Starting ALLSTART.ps1 (detached - it stays up with Presence)..."
try {
    # Do not -Wait: ALLSTART blocks on start-desktopgui.ps1 / dotnet run.
    $start = Start-Process -FilePath $powershellExe `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $AllStart) `
        -WorkingDirectory $RepoRoot `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $AllStartOut `
        -RedirectStandardError $AllStartErr
    if ($null -eq $start) {
        throw 'failed to start ALLSTART process'
    }
    Write-RestartLog "ALLSTART started PID $($start.Id) (out=$AllStartOut)"
    Write-RestartLog 'restart-stack OK - Presence should return via ALLSTART'
}
catch {
    Write-RestartLog "ALLSTART failed: $($_.Exception.Message)"
    throw
}

exit 0
