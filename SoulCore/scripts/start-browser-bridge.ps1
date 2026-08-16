#Requires -Version 5.1
<#
.SYNOPSIS
  Start House Victoria BrowserCaptureBridge on loopback :17891 (BED-182).
.DESCRIPTION
  Runs repo-root BrowserCaptureBridge/bridge_server.py. Soft-fail friendly for ALLSTART.
  Pair with unpacked BrowserCaptureExtension (chrome://extensions → Load unpacked).
#>
[CmdletBinding()]
param(
    [int]$Port = 17891,
    [switch]$ForceRestart
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptsDir)
$BridgePy = Join-Path $RepoRoot "BrowserCaptureBridge\bridge_server.py"
$ReqFile = Join-Path $RepoRoot "BrowserCaptureBridge\requirements.txt"
$PidFile = Join-Path $ScriptsDir ".browser-bridge.pid"
$LogFile = Join-Path $ScriptsDir ".browser-bridge.log"
$HealthUrl = "http://127.0.0.1:${Port}/health"

if (-not (Test-Path -LiteralPath $BridgePy)) {
    throw "Missing bridge: $BridgePy"
}

function Test-BridgeHealthy {
    try {
        $r = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 2 -ErrorAction Stop
        return ($null -ne $r -and $r.ok -eq $true)
    } catch { return $false }
}

if ((-not $ForceRestart) -and (Test-BridgeHealthy)) {
    Write-Host "[start-browser-bridge] Already healthy at $HealthUrl"
    exit 0
}

if ($ForceRestart -and (Test-Path -LiteralPath $PidFile)) {
    try {
        $old = [int](Get-Content -LiteralPath $PidFile -Raw).Trim()
        if ($old -gt 0) {
            Stop-Process -Id $old -Force -ErrorAction SilentlyContinue
        }
    } catch { }
}

function Resolve-Python {
    foreach ($c in @(
        "$env:LOCALAPPDATA\hermes\hermes-agent\venv\Scripts\python.exe",
        "$env:LOCALAPPDATA\hermes\hermes-agent\.venv\Scripts\python.exe",
        "V:\Python311\python.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.12-64\python.exe"
    )) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    $cmd = Get-Command python -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    try {
        $py = & py -3 -c "import sys; print(sys.executable)" 2>$null
        if ($LASTEXITCODE -eq 0 -and $py) { return $py.Trim() }
    } catch { }
    return $null
}

$python = Resolve-Python
if (-not $python) {
    throw "python.exe not found — install Python 3.11+ or ensure it is on PATH"
}
Write-Host "[start-browser-bridge] python: $python"

# Best-effort deps (idempotent).
try {
    & $python -m pip install -q -r $ReqFile 2>$null | Out-Null
} catch {
    Write-Warning "pip install bridge deps failed — if /health fails, run: $python -m pip install -r $ReqFile"
}

if (Test-Path -LiteralPath $LogFile) {
    Clear-Content -LiteralPath $LogFile -ErrorAction SilentlyContinue
}

Write-Host "[start-browser-bridge] Starting $BridgePy on :$Port ..."
$proc = Start-Process -FilePath $python `
    -ArgumentList @($BridgePy) `
    -WorkingDirectory (Split-Path -Parent $BridgePy) `
    -WindowStyle Hidden `
    -RedirectStandardOutput $LogFile `
    -RedirectStandardError $LogFile `
    -PassThru

$proc.Id | Set-Content -LiteralPath $PidFile -Encoding ascii

$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 250
    if ($proc.HasExited) {
        throw "browser bridge exited early (code $($proc.ExitCode)). See $LogFile"
    }
    if (Test-BridgeHealthy) {
        $ready = $true
        break
    }
}

if ($ready) {
    Write-Host "[start-browser-bridge] Healthy: $HealthUrl"
    Write-Host "[start-browser-bridge] Extension: chrome://extensions → Load unpacked → $(Join-Path $RepoRoot 'BrowserCaptureExtension')"
} else {
    Write-Warning "Bridge process started (PID $($proc.Id)) but /health not confirmed yet. Log: $LogFile"
}
