#Requires -Version 5.1
<#
.SYNOPSIS
  Stop every local SoulCore.Host process (loopback :7700 listener + orphans).
.NOTES
  Safe local only: only targets 127.0.0.1 listeners and processes whose command
  line is SoulCore.Host. Never touches Cursor or non-loopback occupants.
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

function Get-SoulCoreHostPids {
    $pids = New-Object 'System.Collections.Generic.HashSet[int]'
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='SoulCore.Host.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object {
            $cl = [string]$_.CommandLine
            if ([string]::IsNullOrWhiteSpace($cl)) { return }
            if ($cl -notmatch 'SoulCore\.Host(\.dll|\.exe)?') { return }
            # Never kill Cursor / agent hosts that merely mention the string in args elsewhere.
            if ($cl -match '(?i)[\\/]Cursor[\\/]|cursor-agent') { return }
            [void]$pids.Add([int]$_.ProcessId)
        }
    return @($pids)
}

function Stop-PidSafe {
    param([int]$ProcId, [string]$Reason)
    if ($ProcId -le 0) { return $false }
    $p = Get-Process -Id $ProcId -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $false }
    $name = $p.ProcessName
    if ($name -match '(?i)^Cursor$') {
        Write-Warning "Skipping Cursor PID $ProcId ($Reason)"
        return $false
    }
    Write-Host "Stopping PID $ProcId ($name) - $Reason"
    Stop-Process -Id $ProcId -Force -ErrorAction SilentlyContinue
    return $true
}

$stopped = 0

# 1) Command-line Host orphans (the herd that never held :7700).
foreach ($procId in @(Get-SoulCoreHostPids)) {
    if (Stop-PidSafe -ProcId $procId -Reason "SoulCore.Host process") {
        $stopped++
    }
}

# 2) Pid file (may already be dead after step 1).
if (Test-Path -LiteralPath $PidFile) {
    $raw = (Get-Content -LiteralPath $PidFile -ErrorAction SilentlyContinue | Select-Object -First 1)
    $filePid = 0
    if ([int]::TryParse("$raw", [ref]$filePid) -and $filePid -gt 0) {
        if (Stop-PidSafe -ProcId $filePid -Reason "pid file") { $stopped++ }
    }
    Remove-Item -LiteralPath $PidFile -Force -ErrorAction SilentlyContinue
}

# 3) Anything still listening on loopback :Port (belt/suspenders).
foreach ($procId in @(Get-LoopbackListenerPids -LocalPort $Port -Address $BindAddress)) {
    if (Stop-PidSafe -ProcId $procId -Reason "listener on ${BindAddress}:${Port}") {
        $stopped++
    }
}

if ($stopped -gt 0) {
    Start-Sleep -Milliseconds 500
}

$stillHost = @(Get-SoulCoreHostPids)
$stillListen = @(Get-LoopbackListenerPids -LocalPort $Port -Address $BindAddress)
if ($stillHost.Count -gt 0 -or $stillListen.Count -gt 0) {
    throw ("SoulCore.Host still present after stop - hosts=[{0}] listeners=[{1}]" -f `
        ($stillHost -join ','), ($stillListen -join ','))
}

Write-Host "SoulCore stopped (no Host processes; ${BindAddress}:${Port} free)"
exit 0
