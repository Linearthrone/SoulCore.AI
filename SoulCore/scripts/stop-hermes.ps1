#Requires -Version 5.1
<#
.SYNOPSIS
  Stop the Hermes gateway bound to loopback :8642.
.NOTES
  Only targets listeners on 127.0.0.1 (never 0.0.0.0).
#>
[CmdletBinding()]
param(
    [int]$Port = 8642
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$PidFile = Join-Path $ScriptsDir ".hermes.pid"
$BindAddress = "127.0.0.1"

function Get-LoopbackListenerPids {
    param([int]$LocalPort, [string]$Address)
    $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $Address }
    if (-not $conns) { return @() }
    return @($conns | Select-Object -ExpandProperty OwningProcess -Unique)
}

$pids = @(Get-LoopbackListenerPids -LocalPort $Port -Address $BindAddress)

if (Test-Path -LiteralPath $PidFile) {
    $filePid = [int](Get-Content -LiteralPath $PidFile -Raw).Trim()
    if ($filePid -gt 0) {
        $proc = Get-Process -Id $filePid -ErrorAction SilentlyContinue
        if ($proc -and ($pids -notcontains $filePid) -and ($pids.Count -eq 0)) {
            Write-Host "Stopping orphaned Hermes PID from file: $filePid"
            Stop-Process -Id $filePid -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($pids.Count -eq 0) {
    Write-Host "No process listening on ${BindAddress}:${Port}"
    if (Test-Path -LiteralPath $PidFile) { Remove-Item -LiteralPath $PidFile -Force }
    exit 0
}

foreach ($procId in $pids) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    $name = if ($p) { $p.ProcessName } else { "?" }
    Write-Host "Stopping PID $procId ($name) bound to ${BindAddress}:${Port}"
    Stop-Process -Id $procId -Force
}

Start-Sleep -Milliseconds 400
if (Test-Path -LiteralPath $PidFile) { Remove-Item -LiteralPath $PidFile -Force }
Write-Host "Hermes stopped."
exit 0
