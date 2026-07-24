#Requires -Version 5.1
<#
.SYNOPSIS
  Stop the SoulCore.Host process bound to loopback :7700.
.NOTES
  Safe local only: only targets listeners on 127.0.0.1 (never 0.0.0.0).
#>
[CmdletBinding()]
param(
    [int]$Port = 7700
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$PidFile = Join-Path $ScriptsDir ".soulcore-host.pid"
$BindAddress = "127.0.0.1"

function Get-LoopbackListenerPids {
    param([int]$LocalPort, [string]$Address)
    $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $Address }
    if (-not $conns) { return @() }
    return @($conns | Select-Object -ExpandProperty OwningProcess -Unique)
}

$pids = @(Get-LoopbackListenerPids -LocalPort $Port -Address $BindAddress)

# Also honor PID file if process still alive and matches loopback listener (or orphaned).
if (Test-Path -LiteralPath $PidFile) {
    $filePid = [int](Get-Content -LiteralPath $PidFile -Raw).Trim()
    if ($filePid -gt 0) {
        $proc = Get-Process -Id $filePid -ErrorAction SilentlyContinue
        if ($proc) {
            if ($pids -notcontains $filePid) {
                # Only kill PID-file process if it is still the loopback :Port owner,
                # or if nothing is listening (orphaned host that failed to bind).
                if ($pids.Count -eq 0) {
                    Write-Host "Stopping orphaned SoulCore PID from file: $filePid"
                    Stop-Process -Id $filePid -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
}

if ($pids.Count -eq 0) {
    Write-Host "No process listening on ${BindAddress}:${Port}"
    if (Test-Path -LiteralPath $PidFile) { Remove-Item -LiteralPath $PidFile -Force }
    exit 0
}

# Refuse to touch non-loopback owners: we only collected 127.0.0.1 listeners.
foreach ($procId in $pids) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    $name = if ($p) { $p.ProcessName } else { "?" }
    Write-Host "Stopping PID $procId ($name) bound to ${BindAddress}:${Port}"
    Stop-Process -Id $procId -Force
}

Start-Sleep -Milliseconds 400
$still = @(Get-LoopbackListenerPids -LocalPort $Port -Address $BindAddress)
if ($still.Count -gt 0) {
    throw "Port ${BindAddress}:${Port} still has listeners: $($still -join ', ')"
}

if (Test-Path -LiteralPath $PidFile) { Remove-Item -LiteralPath $PidFile -Force }
Write-Host "SoulCore stopped (${BindAddress}:${Port} free)"
