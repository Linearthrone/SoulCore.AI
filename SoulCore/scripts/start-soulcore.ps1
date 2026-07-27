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

if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
    Write-Host "SoulCore already listening on ${BindAddress}:${Port}"
    Write-Host "Health: $HealthUrl"
    exit 0
}

$dll = Join-Path $SoulCoreRoot "SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll"
$needBuild = $ForceRebuild -or -not (Test-Path -LiteralPath $dll)

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
# (child inherits). Never log values. Skip comments/blank; do not overwrite non-empty env.
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
        $existing = [Environment]::GetEnvironmentVariable($key, "Process")
        if (-not [string]::IsNullOrEmpty($existing)) { continue }
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
    Write-Host "loaded $loadedCount SOULCORE_* keys from .env"
} else {
    Write-Host ".env not found at $EnvFile (skipping SOULCORE_* load)"
}

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

$ready = $false
for ($i = 0; $i -lt 40; $i++) {
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
    Write-Warning "Process started (PID $($proc.Id)) but port not confirmed listening yet."
}

Write-Host "PID: $($proc.Id)"
Write-Host "PID file: $PidFile"
Write-Host "Log: $LogFile"
Write-Host "Health: $HealthUrl"
