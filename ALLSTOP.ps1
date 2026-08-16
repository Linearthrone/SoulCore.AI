#Requires -Version 5.1
<#
.SYNOPSIS
  Stop House.ChatDesktop and local SoulCore.Host (Victoria).
.DESCRIPTION
  Does not kill foreign :7700 occupants (e.g. Cursor cloud port-forward).
  Stops local Host on 7700 and/or 7701 when /health memory path is this machine's Victoria.
  Also stops browser bridge (:17891) and local voice STT/TTS (:8000 / :8881).
  Does not start or stop Hermes - that stack is retired from Victoria.
.EXAMPLE
  .\ALLSTOP.ps1
#>
[CmdletBinding()]
param(
    [int[]]$Ports = @(7700, 7701),
    [switch]$KeepVoice,
    [switch]$KeepBrowserBridge
)

$ErrorActionPreference = "Continue"
$RepoRoot = $PSScriptRoot
$StopHost = Join-Path $RepoRoot "SoulCore\scripts\stop-soulcore.ps1"
$PidFile = Join-Path $RepoRoot "SoulCore\scripts\.soulcore-host.pid"
$BindAddress = "127.0.0.1"
$TailscaleServe = Join-Path $RepoRoot "SoulCore\scripts\tailscale-serve-soulcore.ps1"

function Test-LocalVictoriaHealth {
    param($Health)
    if ($null -eq $Health) { return $false }
    if ("$($Health.service)" -ne "SoulCore.Host") { return $false }
    $path = [string]$Health.memory.path
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    $expectedRoot = Join-Path $env:LOCALAPPDATA "SoulCore"
    if ($path.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($path -match '(?i)\\AppData\\Local\\SoulCore\\') { return $true }
    return $false
}

function Get-HealthObject {
    param([int]$LocalPort)
    try {
        return Invoke-RestMethod -Uri "http://127.0.0.1:${LocalPort}/health" -TimeoutSec 2 -ErrorAction Stop
    } catch {
        return $null
    }
}

function Get-LoopbackListenerPids {
    param([int]$LocalPort)
    $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $BindAddress }
    if (-not $conns) { return @() }
    return @($conns | Select-Object -ExpandProperty OwningProcess -Unique)
}

function Stop-ChatDesktop {
    Write-Host "=== ALLSTOP: House.ChatDesktop ==="
    $stopped = 0

    # Built WinExe
    Get-Process -Name "House.ChatDesktop" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping House.ChatDesktop PID $($_.Id)"
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        $stopped++
    }

    # dotnet run --project House.ChatDesktop
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $cl = [string]$_.CommandLine
            $cl -match 'House\.ChatDesktop' -or $cl -match 'House\\House\.ChatDesktop'
        } |
        ForEach-Object {
            Write-Host "Stopping dotnet ChatDesktop PID $($_.ProcessId)"
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
            $stopped++
        }

    if ($stopped -eq 0) {
        Write-Host "No ChatDesktop process found."
    } else {
        Write-Host "Stopped $stopped ChatDesktop-related process(es)."
    }
}

function Stop-LocalHostOnPort {
    param([int]$LocalPort)

    $health = Get-HealthObject -LocalPort $LocalPort
    if ($null -eq $health) {
        Write-Host "No Host answering on ${BindAddress}:${LocalPort}"
        return
    }

    if (-not (Test-LocalVictoriaHealth -Health $health)) {
        Write-Warning "Leaving :${LocalPort} alone - not local Victoria (memory=$($health.memory.path))"
        return
    }

    $pids = @(Get-LoopbackListenerPids -LocalPort $LocalPort)
    if ($pids.Count -eq 0) {
        Write-Host "Local Victoria answered :${LocalPort} but no loopback listener PID found."
        return
    }

    foreach ($procId in $pids) {
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        $name = if ($p) { $p.ProcessName } else { "?" }
        if ($name -match '(?i)^Cursor$') {
            Write-Warning "Skipping Cursor PID $procId on :${LocalPort}"
            continue
        }
        Write-Host "Stopping local Victoria Host PID $procId ($name) on :${LocalPort}"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
}

function Stop-ListenersOnPort {
    param(
        [Parameter(Mandatory = $true)][int]$LocalPort,
        [Parameter(Mandatory = $true)][string]$Label
    )
    $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $BindAddress -or $_.LocalAddress -eq "0.0.0.0" }
    $pids = @($conns | Select-Object -ExpandProperty OwningProcess -Unique)
    if ($pids.Count -eq 0) {
        Write-Host "No $Label listener on :${LocalPort}"
        return
    }
    foreach ($procId in $pids) {
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        $name = if ($p) { $p.ProcessName } else { "?" }
        Write-Host "Stopping $Label PID $procId ($name) on :${LocalPort}"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "=== ALLSTOP ==="
Stop-ChatDesktop

if ($KeepBrowserBridge) {
    Write-Host "=== ALLSTOP: browser bridge kept (-KeepBrowserBridge) ==="
} else {
    Write-Host "=== ALLSTOP: browser bridge (:17891) ==="
    Stop-ListenersOnPort -LocalPort 17891 -Label "browser bridge"
    $bbPid = Join-Path $RepoRoot "SoulCore\scripts\.browser-bridge.pid"
    if (Test-Path -LiteralPath $bbPid) {
        Remove-Item -LiteralPath $bbPid -Force -ErrorAction SilentlyContinue
    }
}

if ($KeepVoice) {
    Write-Host "=== ALLSTOP: voice kept (-KeepVoice) ==="
} else {
    Write-Host "=== ALLSTOP: House.Voice (STT :8000 + TTS :8881) ==="
    Stop-ListenersOnPort -LocalPort 8000 -Label "STT"
    Stop-ListenersOnPort -LocalPort 8881 -Label "TTS"
}

# --- Tailscale serve: tear down (soft-fail) ---
if (-not (Test-Path -LiteralPath $TailscaleServe)) {
    Write-Warning "tailscale-serve-soulcore.ps1 not found - skipping serve teardown"
} else {
    Write-Host "=== ALLSTOP: Tailscale serve ==="
    try {
        $tsArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$TailscaleServe,"-Off")
        $tsProc = Start-Process -FilePath "powershell.exe" `
            -ArgumentList $tsArgs `
            -WorkingDirectory $RepoRoot `
            -Wait -PassThru -NoNewWindow
        if ($tsProc.ExitCode -ne 0) {
            Write-Warning "tailscale-serve-soulcore.ps1 -Off exited $($tsProc.ExitCode) - continuing"
        }
    } catch {
        Write-Warning "Tailscale serve teardown failed - $($_.Exception.Message)"
    }
}

Write-Host "=== ALLSTOP: SoulCore.Host (local Victoria only) ==="
foreach ($port in $Ports) {
    Stop-LocalHostOnPort -LocalPort $port
}

# Pid-file orphan cleanup via existing stop script (7700 default), then alt port.
if (Test-Path -LiteralPath $StopHost) {
    foreach ($port in $Ports) {
        $health = Get-HealthObject -LocalPort $port
        if ($null -eq $health -or (Test-LocalVictoriaHealth -Health $health)) {
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $StopHost -Port $port 2>$null | Out-Host
        }
    }
}

if (Test-Path -LiteralPath $PidFile) {
    Remove-Item -LiteralPath $PidFile -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Milliseconds 300
Write-Host "=== ALLSTOP done ==="
foreach ($port in $Ports) {
    $h = Get-HealthObject -LocalPort $port
    if ($null -eq $h) {
        Write-Host ":${port} free (no /health)"
    } elseif (Test-LocalVictoriaHealth -Health $h) {
        Write-Warning ":${port} still local Victoria - may need manual kill"
    } else {
        Write-Host ":${port} still occupied by foreign Host (left alone)"
    }
}
