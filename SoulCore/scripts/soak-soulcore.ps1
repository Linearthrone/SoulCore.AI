#Requires -Version 5.1
<#
.SYNOPSIS
  Short continuity soak: loop /health probes and record PID stability.
.DESCRIPTION
  Default 15 minutes. Not a full 24h soak. Loopback only (SEC-004).
  See SoulCore/docs/soak-runbook.md for abort criteria and House reconnect steps.
.PARAMETER Minutes
  Soak duration in minutes (default 15).
.PARAMETER IntervalSeconds
  Seconds between health probes (default 15).
.PARAMETER FailStreakAbort
  Consecutive health failures before abort (default 3).
.PARAMETER MinFreeMb
  Abort if LocalAppData volume free space drops below this (default 200).
.PARAMETER SkipStart
  Do not auto-start Host if port is down.
#>
[CmdletBinding()]
param(
    [int]$Minutes = 15,
    [int]$IntervalSeconds = 15,
    [int]$FailStreakAbort = 3,
    [int]$MinFreeMb = 200,
    [switch]$SkipStart
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$StartScript = Join-Path $ScriptsDir "start-soulcore.ps1"
$BindAddress = "127.0.0.1"
$Port = 7700
$HealthUrl = "http://${BindAddress}:${Port}/health"
$LogsDir = Join-Path $ScriptsDir "logs"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogFile = Join-Path $LogsDir "soak-$stamp.log"

if ($Minutes -lt 1) { throw 'Minutes must be at least 1' }
if ($IntervalSeconds -lt 1) { throw 'IntervalSeconds must be at least 1' }

New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null

function Write-Soak {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Add-Content -LiteralPath $LogFile -Value $line -Encoding utf8
    Write-Host $line
}

function Get-LoopbackListenerPid {
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $BindAddress }
    if (-not $conns) { return $null }
    return @($conns | Select-Object -ExpandProperty OwningProcess -Unique)[0]
}

function Get-LocalAppDataFreeMb {
    $root = [Environment]::GetFolderPath("LocalApplicationData")
    $driveLetter = (Split-Path -Qualifier $root).TrimEnd(":")
    $drive = Get-PSDrive -Name $driveLetter -ErrorAction SilentlyContinue
    if (-not $drive) { return $null }
    return [math]::Round(($drive.Free / 1MB), 1)
}

function Test-Health {
    try {
        $resp = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 5
        return @{
            Ok         = ($resp.StatusCode -eq 200)
            StatusCode = [int]$resp.StatusCode
            Body       = $resp.Content
        }
    } catch {
        return @{
            Ok         = $false
            StatusCode = 0
            Body       = $_.Exception.Message
        }
    }
}

# Ensure Host up
$listenPid = Get-LoopbackListenerPid
if (-not $listenPid) {
    if ($SkipStart) {
        throw "No listener on ${BindAddress}:${Port} and -SkipStart was set."
    }
    Write-Soak "Host down - invoking start-soulcore.ps1"
    & $StartScript
    Start-Sleep -Seconds 1
    $listenPid = Get-LoopbackListenerPid
    if (-not $listenPid) {
        throw "Host failed to listen on ${BindAddress}:${Port} after start."
    }
}

$startHealth = Test-Health
if (-not $startHealth.Ok) {
    throw "Initial health failed: $($startHealth.Body)"
}

$baselinePid = $listenPid
$probeCount = 0
$okCount = 0
$failStreak = 0
$maxFailStreak = 0
$pidChanges = 0
$abortReason = $null
$startedAt = Get-Date
$deadline = $startedAt.AddMinutes($Minutes)

function Format-HealthSummary {
    param([string]$Body)
    try {
        $j = $Body | ConvertFrom-Json
        $mem = if ($null -ne $j.memory) { $j.memory.open } else { "?" }
        $unreal = if ($null -ne $j.unreal) { $j.unreal.connected } else { "?" }
        return ("status={0} bind={1} port={2} memOpen={3} unrealConnected={4}" -f `
            $j.status, $j.bind, $j.port, $mem, $unreal)
    } catch {
        return ("rawChars={0}" -f $Body.Length)
    }
}

Write-Soak "=== SoulCore soak start ==="
Write-Soak "Minutes=$Minutes IntervalSeconds=$IntervalSeconds FailStreakAbort=$FailStreakAbort MinFreeMb=$MinFreeMb"
Write-Soak "HealthUrl=<$HealthUrl> BaselinePid=$baselinePid"
Write-Soak ("InitialHealth {0}" -f (Format-HealthSummary $startHealth.Body))
Write-Soak "LogFile=$LogFile"

while ((Get-Date) -lt $deadline) {
    $probeCount++
    $nowPid = Get-LoopbackListenerPid
    $freeMb = Get-LocalAppDataFreeMb
    $health = Test-Health

    $pidNote = if ($null -eq $nowPid) { "NONE" } else { "$nowPid" }
    if ($null -ne $nowPid -and $nowPid -ne $baselinePid) {
        $pidChanges++
        Write-Soak ("ABORT candidate: PID changed baseline={0} now={1} changeCount={2}" -f $baselinePid, $nowPid, $pidChanges)
        $abortReason = "port_steal_or_restart: PID changed from $baselinePid to $nowPid"
        break
    }

    # Non-loopback listen check
    $foreign = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -ne $BindAddress }
    if ($foreign) {
        $addrs = ($foreign | ForEach-Object { $_.LocalAddress }) -join ","
        $abortReason = "non_loopback_bind: $addrs"
        Write-Soak "ABORT: $abortReason"
        break
    }

    if ($null -ne $freeMb -and $freeMb -lt $MinFreeMb) {
        $abortReason = ("disk_low: LocalAppData volume free {0}MB below {1}MB" -f $freeMb, $MinFreeMb)
        Write-Soak "ABORT: $abortReason"
        break
    }

    if ($health.Ok) {
        $okCount++
        $failStreak = 0
        $memOpen = $true
        try {
            $json = $health.Body | ConvertFrom-Json
            if ($json.bind -and $json.bind -ne $BindAddress) {
                $abortReason = "health_bind_not_loopback: $($json.bind)"
                Write-Soak "ABORT: $abortReason"
                break
            }
            if ($json.memory -and ($json.memory.open -eq $false)) {
                $memOpen = $false
                $abortReason = "memory_db_not_open"
                Write-Soak "ABORT: $abortReason body=$($health.Body)"
                break
            }
        } catch {
            # non-JSON body still counted as HTTP OK; log only
        }
        Write-Soak ("PROBE {0} OK status={1} pid={2} freeMb={3} memOpen={4}" -f $probeCount, $health.StatusCode, $pidNote, $freeMb, $memOpen)
    } else {
        $failStreak++
        if ($failStreak -gt $maxFailStreak) { $maxFailStreak = $failStreak }
        Write-Soak ("PROBE {0} FAIL streak={1} pid={2} err={3}" -f $probeCount, $failStreak, $pidNote, $health.Body)
        if ($null -eq $nowPid) {
            $abortReason = "crash: no listener on ${BindAddress}:${Port}"
            Write-Soak "ABORT: $abortReason"
            break
        }
        if ($failStreak -ge $FailStreakAbort) {
            $abortReason = "health_fail_streak=$failStreak"
            Write-Soak "ABORT: $abortReason"
            break
        }
    }

    $remaining = ($deadline - (Get-Date)).TotalSeconds
    if ($remaining -le 0) { break }
    $sleepSec = [math]::Min($IntervalSeconds, [math]::Ceiling($remaining))
    Start-Sleep -Seconds $sleepSec
}

$finalPid = Get-LoopbackListenerPid
$finalHealth = Test-Health
$elapsedMin = [math]::Round(((Get-Date) - $startedAt).TotalMinutes, 2)

Write-Soak "=== SoulCore soak end ==="
Write-Soak ("ElapsedMinutes={0} Probes={1} Ok={2} MaxFailStreak={3} PidChanges={4}" -f $elapsedMin, $probeCount, $okCount, $maxFailStreak, $pidChanges)
Write-Soak "BaselinePid=$baselinePid FinalPid=$finalPid"
Write-Soak ("FinalHealthOk={0} {1}" -f $finalHealth.Ok, (Format-HealthSummary $finalHealth.Body))
if ($abortReason) {
    Write-Soak "AbortReason=$abortReason"
}

$result = [ordered]@{
    pass           = ($null -eq $abortReason -and $okCount -gt 0 -and $finalHealth.Ok -and $finalPid -eq $baselinePid)
    abortReason    = $abortReason
    minutes        = $Minutes
    probes         = $probeCount
    ok             = $okCount
    maxFailStreak  = $maxFailStreak
    pidChanges     = $pidChanges
    baselinePid    = $baselinePid
    finalPid       = $finalPid
    logFile        = $LogFile
}

$summaryJson = ($result | ConvertTo-Json -Compress)
Write-Soak "SUMMARY $summaryJson"

if (-not $result.pass) {
    Write-Error "Soak FAILED: $abortReason (log: $LogFile)"
    exit 1
}

Write-Host ("Soak PASS - PID stable ({0}), {1}/{2} health OK. Log: {3}" -f $baselinePid, $okCount, $probeCount, $LogFile)
exit 0
