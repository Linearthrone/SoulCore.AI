#Requires -Version 5.1
<#
.SYNOPSIS
  Build (if needed) and start SoulCore.Host on loopback :7700.
.DESCRIPTION
  Inference/embed preflight (TASK-112): before starting the Host, probe the
  local Ollama server (/api/tags) and confirm the embedding model is present.
    * Ollama unreachable  -> WARNING only (Host still starts; it can serve
      health/WS stubs offline). Semantic recall/embeddings will be degraded.
    * Model missing        -> WARNING with remedy `ollama pull <model>`.
                              Use -PullEmbedModel to auto-pull (opt-in only).
    * All good             -> confirmation line, no behavior change.
  Embedding model name resolves from env SOULCORE_EMBED_MODEL (loaded from
  SoulCore/.env if present), defaulting to `nomic-embed-text`. Warnings never
  print secrets/API keys. Preflight is advisory and does not hard-fail start.
.NOTES
  SEC-004: V1 binds 127.0.0.1 only. Does not use 0.0.0.0.
#>
[CmdletBinding()]
param(
    [int]$Port = 7700,
    [switch]$ForceRebuild,
    # Stop any Host already on this port (pid file + listeners) then start fresh.
    # Needed after .env / branch changes - ALLSTART otherwise reuses a stale Host.
    [switch]$RestartHost,
    # Opt-in: run `ollama pull <embed-model>` when the model is missing.
    [switch]$PullEmbedModel,
    # Base URL of the local Ollama server used for the preflight probe.
    [string]$OllamaUrl = "http://127.0.0.1:11434",
    # Skip the Ollama/embed preflight entirely.
    [switch]$SkipPreflight
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$SoulCoreRoot = Split-Path -Parent $ScriptsDir
$HostProject = Join-Path $SoulCoreRoot "SoulCore.Host\SoulCore.Host.csproj"
$Sln = Join-Path $SoulCoreRoot "SoulCore.sln"
$PidFile = Join-Path $ScriptsDir ".soulcore-host.pid"
$LogFile = Join-Path $ScriptsDir ".soulcore-host.log"
$BindAddress = "127.0.0.1"
$HealthUrl = "http://${BindAddress}:${Port}/health"

if (-not (Test-Path -LiteralPath $HostProject)) {
    throw "Host project not found: $HostProject"
}

function Test-PortListening {
    param([int]$LocalPort, [string]$Address)
    try {
        $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.LocalAddress -eq $Address }
        return $null -ne $conns
    } catch {
        return $false
    }
}

function Get-LoopbackListenerPids {
    param([int]$LocalPort, [string]$Address)
    try {
        $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.LocalAddress -eq $Address }
        if (-not $conns) { return @() }
        return @($conns | Select-Object -ExpandProperty OwningProcess -Unique)
    } catch {
        return @()
    }
}

function Get-SoulCoreHostPids {
    $pids = New-Object 'System.Collections.Generic.HashSet[int]'
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='SoulCore.Host.exe'" -ErrorAction SilentlyContinue |
        ForEach-Object {
            $cl = [string]$_.CommandLine
            if ([string]::IsNullOrWhiteSpace($cl)) { return }
            if ($cl -notmatch 'SoulCore\.Host(\.dll|\.exe)?') { return }
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
    if ($p.ProcessName -match '(?i)^Cursor$') {
        Write-Warning "Skipping Cursor PID $ProcId ($Reason)"
        return $false
    }
    Write-Host "Stopping PID $ProcId ($($p.ProcessName)) - $Reason"
    Stop-Process -Id $ProcId -Force -ErrorAction SilentlyContinue
    return $true
}

<#
  Inventory before start: keep at most one healthy :Port Host.
  Kill every other SoulCore.Host process (the herd that caused 11 orphans).
#>
function Stop-SoulCoreHostOrphans {
    param(
        [int]$LocalPort,
        [string]$Address,
        [switch]$KillListenerToo
    )
    $listeners = @(Get-LoopbackListenerPids -LocalPort $LocalPort -Address $Address)
    $hosts = @(Get-SoulCoreHostPids)
    $keep = @()
    if (-not $KillListenerToo -and $listeners.Count -eq 1) {
        $keep = @($listeners[0])
    }

    $killed = 0
    foreach ($procId in $hosts) {
        if ($keep -contains $procId) { continue }
        if (Stop-PidSafe -ProcId $procId -Reason "orphan SoulCore.Host (not sole :$LocalPort listener)") {
            $killed++
        }
    }

    if ($KillListenerToo) {
        foreach ($procId in $listeners) {
            if (Stop-PidSafe -ProcId $procId -Reason "listener on ${Address}:${LocalPort}") {
                $killed++
            }
        }
    } elseif ($listeners.Count -gt 1) {
        Write-Warning "Multiple listeners on ${Address}:${LocalPort} - clearing all and requiring a fresh start"
        foreach ($procId in $listeners) {
            if (Stop-PidSafe -ProcId $procId -Reason "duplicate listener") { $killed++ }
        }
    }

    if ($killed -gt 0) {
        Write-Host "Reaped $killed leftover Host process(es) before start"
        Start-Sleep -Milliseconds 600
    } else {
        Write-Host "Host inventory: $($hosts.Count) process(es), $($listeners.Count) listener(s) on :$LocalPort"
    }
    return $killed
}

# TASK-112: Advisory Ollama/embed preflight. Never hard-fails Host start.
# Probes <OllamaUrl>/api/tags and confirms the embedding model is available.
function Invoke-InferencePreflight {
    param(
        [string]$BaseUrl,
        [switch]$Pull
    )

    $model = [Environment]::GetEnvironmentVariable("SOULCORE_EMBED_MODEL", "Process")
    if ([string]::IsNullOrWhiteSpace($model)) { $model = "nomic-embed-text" }
    $model = $model.Trim()

    $tagsUrl = ($BaseUrl.TrimEnd('/')) + "/api/tags"
    Write-Host "Preflight: probing Ollama at $tagsUrl (embed model: $model)"

    $tags = $null
    try {
        $tags = Invoke-RestMethod -Uri $tagsUrl -Method Get -TimeoutSec 5 -ErrorAction Stop
    } catch {
        Write-Warning "Ollama unreachable at $BaseUrl - $($_.Exception.Message)"
        Write-Warning "Host will still start, but embeddings/semantic recall will be degraded until Ollama is running."
        Write-Warning "Remedy: start Ollama (e.g. 'ollama serve') then re-run this script."
        return
    }

    $names = @()
    if ($null -ne $tags -and $null -ne $tags.models) {
        $names = @($tags.models | ForEach-Object { $_.name })
    }

    $present = $false
    foreach ($n in $names) {
        if ([string]::IsNullOrWhiteSpace($n)) { continue }
        if ($n -eq $model -or $n -eq "${model}:latest" -or $n -like "${model}:*") {
            $present = $true
            break
        }
    }

    if ($present) {
        Write-Host "Preflight OK: Ollama reachable and embed model '$model' is installed."
        return
    }

    Write-Warning "Embed model '$model' not found in Ollama tags."
    if ($Pull) {
        Write-Host "Preflight: -PullEmbedModel set -> running 'ollama pull $model' ..."
        try {
            & ollama pull $model
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "'ollama pull $model' exited with code $LASTEXITCODE. Embeddings may be unavailable."
            } else {
                Write-Host "Preflight: pull complete for '$model'."
            }
        } catch {
            Write-Warning "Failed to run 'ollama pull $model' - $($_.Exception.Message)"
            Write-Warning "Is the Ollama CLI on PATH? Remedy: ollama pull $model"
        }
    } else {
        Write-Warning "Remedy: ollama pull $model   (or re-run with -PullEmbedModel to auto-pull)"
        Write-Warning "Host will still start; episodics may be written without embedding vectors until the model is present."
    }
}

function Stop-SoulCoreOnPort {
    param([int]$LocalPort, [string]$Address, [string]$PidPath)
    # Full clear: every Host process + listener (RestartHost / failed bind recovery).
    [void](Stop-SoulCoreHostOrphans -LocalPort $LocalPort -Address $Address -KillListenerToo)
    if (Test-Path -LiteralPath $PidPath) {
        Remove-Item -LiteralPath $PidPath -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 400
}

if ($RestartHost) {
    Write-Host "RestartHost: clearing every SoulCore.Host (+ :${Port} listener)"
    Stop-SoulCoreOnPort -LocalPort $Port -Address $BindAddress -PidPath $PidFile
    # Second pass - pid-file miss / slow exit used to leave the port busy, then the
    # "already listening" early-exit skipped rebuild and left /health stuck (e.g. 0.1.6).
    if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
        Write-Host "RestartHost: port still busy - forcing another stop..."
        Stop-SoulCoreOnPort -LocalPort $Port -Address $BindAddress -PidPath $PidFile
        Start-Sleep -Milliseconds 500
    }
    if ((@(Get-SoulCoreHostPids)).Count -gt 0 -or (Test-PortListening -LocalPort $Port -Address $BindAddress)) {
        throw "RestartHost could not free SoulCore.Host / ${BindAddress}:${Port} - refuse to reuse stale Host (Update would lie)."
    }
} else {
    # Always inventory first: reap orphans, keep a single healthy listener if present.
    [void](Stop-SoulCoreHostOrphans -LocalPort $Port -Address $BindAddress)
}

# Only skip work when operator did not ask for rebuild/restart AND one clean Host owns the port.
if (-not $ForceRebuild -and -not $RestartHost -and (Test-PortListening -LocalPort $Port -Address $BindAddress)) {
    $listeners = @(Get-LoopbackListenerPids -LocalPort $Port -Address $BindAddress)
    $hosts = @(Get-SoulCoreHostPids)
    if ($listeners.Count -eq 1 -and $hosts.Count -le 1) {
        Write-Host "SoulCore already listening on ${BindAddress}:${Port} (PID $($listeners[0])) - reusing, not starting another"
        Write-Host "Health: $HealthUrl"
        Write-Host "Tip: after changing SoulCore/.env or source, re-run with -RestartHost (rebuilds Host) so the process reloads."
        exit 0
    }
    Write-Warning "Port :${Port} busy but Host inventory is messy (listeners=$($listeners.Count) hosts=$($hosts.Count)) - clearing and starting fresh"
    Stop-SoulCoreOnPort -LocalPort $Port -Address $BindAddress -PidPath $PidFile
}

$dll = Join-Path $SoulCoreRoot "SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll"
# -RestartHost alone used to reuse a stale DLL (no embedPane / wrong BrowserBackend).
# Always rebuild when restarting or when the DLL is missing.
$needBuild = $ForceRebuild -or $RestartHost -or -not (Test-Path -LiteralPath $dll)

if ($needBuild) {
    Write-Host "Building SoulCore..."
    & dotnet build $Sln -c Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed (exit $LASTEXITCODE)"
    }
}

if (-not (Test-Path -LiteralPath $dll)) {
    throw "Host DLL missing after build: $dll"
}

# Ensure Host config uses loopback (env overrides; Host also refuses non-loopback).
$env:Host__BindAddress = $BindAddress
$env:Host__Port = "$Port"

# Load SOULCORE_* from SoulCore/.env into process env before Start-Process
# (child inherits). Never log values. .env overwrites stale Process/User-inherited
# tokens so companion auth matches the file operator edits (PROP-1.2 401 footgun).
$EnvFile = Join-Path $SoulCoreRoot ".env"
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
    Write-Host "loaded/overwrote $loadedCount SOULCORE_* keys from .env (file wins over stale shell env)"
} else {
    Write-Host ".env not found at $EnvFile (skipping SOULCORE_* load)"
}

$vboxPass = [Environment]::GetEnvironmentVariable("SOULCORE_VBOX_GUEST_PASS", "Process")
$vboxUser = [Environment]::GetEnvironmentVariable("SOULCORE_VBOX_GUEST_USER", "Process")
if ([string]::IsNullOrWhiteSpace($vboxUser)) { $vboxUser = "victoria" }
$vboxPresent = -not [string]::IsNullOrEmpty($vboxPass)
$vboxLen = if ($vboxPresent) { $vboxPass.Length } else { 0 }
Write-Host "guestcontrol: user=$vboxUser SOULCORE_VBOX_GUEST_PASS present=$vboxPresent length=$vboxLen"

# TASK-112: advisory inference/embed preflight (after .env load so
# SOULCORE_EMBED_MODEL is honored). Warnings only; never blocks Host start.
if (-not $SkipPreflight) {
    Invoke-InferencePreflight -BaseUrl $OllamaUrl -Pull:$PullEmbedModel
} else {
    Write-Host "Preflight skipped (-SkipPreflight)."
}

Write-Host "Starting SoulCore.Host on http://${BindAddress}:${Port} ..."
$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList @($dll, "--urls", "http://${BindAddress}:${Port}") `
    -WorkingDirectory (Split-Path -Parent $dll) `
    -WindowStyle Hidden `
    -RedirectStandardOutput $LogFile `
    -RedirectStandardError "${LogFile}.err" `
    -PassThru

$proc.Id | Set-Content -LiteralPath $PidFile -Encoding ascii

# PROP-17.1: start timeout - if Host never binds, force-kill and fail clearly
# so Presence can clear "Starting Host..." (Stop also force-kills via stop-soulcore.ps1).
$StartTimeoutSec = 45
$ready = $false
$deadline = [datetime]::UtcNow.AddSeconds($StartTimeoutSec)
while ([datetime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 250
    if ($proc.HasExited) {
        $err = if (Test-Path "${LogFile}.err") { Get-Content "${LogFile}.err" -Raw } else { "" }
        throw "SoulCore.Host exited early (code $($proc.ExitCode)). stderr:`n$err"
    }
    if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
        $ready = $true
        break
    }
}

if (-not $ready) {
    Write-Warning "Host start timed out after ${StartTimeoutSec}s (PID $($proc.Id)) - force-killing stuck process."
    try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
    if (Test-Path -LiteralPath $PidFile) { Remove-Item -LiteralPath $PidFile -Force -ErrorAction SilentlyContinue }
    throw "SoulCore.Host failed to listen on ${BindAddress}:${Port} within ${StartTimeoutSec}s (force-killed). Stop is always safe via stop-soulcore.ps1."
}

Write-Host "PID: $($proc.Id)"
Write-Host "PID file: $PidFile"
Write-Host "Log: $LogFile"
Write-Host "Health: $HealthUrl"
exit 0
