#Requires -Version 5.1
<#
.SYNOPSIS
  Start Hermes Agent gateway (OpenAI-compatible API) on loopback :8642.
.DESCRIPTION
  OPS-143 restore script (mirrors start-soulcore.ps1 style):
    * Preflight: venv + hermes CLI + optional Ollama probe
    * Starts `hermes gateway run` with HERMES_HOME ~/.hermes
    * Writes pid file (.hermes.pid) and log (.hermes.log)
  Does NOT toggle SoulCore Hermes.Enabled (BED-144 owns that).
.NOTES
  Bind is loopback only via ~/.hermes/.env API_SERVER_HOST=127.0.0.1.
  Linux cloud: prefer start-hermes.sh (same layout); this script is for Windows/pwsh.
#>
[CmdletBinding()]
param(
    [int]$Port = 8642,
    [string]$BindAddress = "127.0.0.1",
    [string]$HermesHome = "",
    [string]$VenvPath = "",
    [switch]$SkipPreflight
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$SoulCoreRoot = Split-Path -Parent $ScriptsDir
$PidFile = Join-Path $ScriptsDir ".hermes.pid"
$LogFile = Join-Path $ScriptsDir ".hermes.log"
$HealthUrl = "http://${BindAddress}:${Port}/health"

if ([string]::IsNullOrWhiteSpace($HermesHome)) {
    $HermesHome = Join-Path $env:USERPROFILE ".hermes"
    if (-not (Test-Path -LiteralPath $HermesHome)) {
        $HermesHome = Join-Path $HOME ".hermes"
    }
}
if ([string]::IsNullOrWhiteSpace($VenvPath)) {
    $VenvPath = Join-Path $SoulCoreRoot ".venv-hermes"
}

$HermesBin = Join-Path $VenvPath "Scripts\hermes.exe"
if (-not (Test-Path -LiteralPath $HermesBin)) {
    $HermesBin = Join-Path $VenvPath "bin\hermes"
}
if (-not (Test-Path -LiteralPath $HermesBin)) {
    throw "Hermes CLI not found under $VenvPath. Create venv and: pip install hermes-agent==0.18.2 aiohttp mcp"
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

if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
    Write-Host "Hermes already listening on ${BindAddress}:${Port}"
    try {
        $h = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 5
        Write-Host ("Health: " + ($h | ConvertTo-Json -Compress))
    } catch {
        Write-Warning "Port open but /health failed: $($_.Exception.Message)"
    }
    exit 0
}

if (-not $SkipPreflight) {
    Write-Host "Preflight: HermesHome=$HermesHome"
    if (-not (Test-Path -LiteralPath (Join-Path $HermesHome "config.yaml"))) {
        Write-Warning "Missing $HermesHome\config.yaml — run hermes setup / copy from runbook."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $HermesHome ".env"))) {
        Write-Warning "Missing $HermesHome\.env — need API_SERVER_ENABLED=true, API_SERVER_HOST=127.0.0.1, API_SERVER_PORT=$Port, API_SERVER_KEY=..."
    }
    try {
        $null = Invoke-RestMethod -Uri "http://127.0.0.1:11434/api/tags" -TimeoutSec 3
        Write-Host "Preflight: Ollama :11434 reachable (model provider for Hermes)."
    } catch {
        Write-Warning "Ollama :11434 unreachable — Hermes gateway can still bind, but chat will fail until a model provider is up."
    }
}

$env:HERMES_HOME = $HermesHome
Write-Host "Starting Hermes gateway via: $HermesBin gateway run"
Write-Host "Log: $LogFile"

$proc = Start-Process -FilePath $HermesBin -ArgumentList @("gateway", "run") `
    -WorkingDirectory $SoulCoreRoot `
    -RedirectStandardOutput $LogFile `
    -RedirectStandardError $LogFile `
    -PassThru -WindowStyle Hidden

$proc.Id | Set-Content -LiteralPath $PidFile -Encoding ascii
Write-Host "PID $($proc.Id) written to $PidFile"

$ok = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 500
    try {
        $h = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 2
        Write-Host ("Health OK: " + ($h | ConvertTo-Json -Compress))
        $ok = $true
        break
    } catch {
        # keep waiting
    }
}

if (-not $ok) {
    Write-Warning "Gateway started but /health did not return within ~15s. Check $LogFile and ~/.hermes/logs/gateway.log"
    exit 1
}

Write-Host "Stop: .\stop-hermes.ps1   Restart: .\stop-hermes.ps1; .\start-hermes.ps1"
exit 0
