#Requires -Version 5.1
<#
.SYNOPSIS
  Start House Victoria BrowserCaptureBridge on loopback :17891 (BED-182).
.DESCRIPTION
  Runs repo-root BrowserCaptureBridge/bridge_server.py. Soft-fail friendly for ALLSTART.
  Pair with unpacked BrowserCaptureExtension (chrome://extensions -> Load unpacked).
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
    # Prefer a normal Python install — never the old Hermes agent venv.
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.12-64\python.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.11-64\python.exe",
        "V:\Python311\python.exe",
        "C:\Python312\python.exe",
        "C:\Python311\python.exe"
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path -LiteralPath $c)) { return $c }
    }
    $cmd = Get-Command python -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source -and ($cmd.Source -notmatch '[\\/]hermes[\\/]')) {
        return $cmd.Source
    }
    try {
        $py = & py -3 -c "import sys; print(sys.executable)" 2>$null
        if ($LASTEXITCODE -eq 0 -and $py) {
            $path = $py.Trim()
            if ($path -notmatch '[\\/]hermes[\\/]') { return $path }
        }
    } catch { }
    return $null
}

$python = Resolve-Python
if (-not $python) {
    throw "python.exe not found - install Python 3.11+ or ensure it is on PATH (not the Hermes venv)"
}
Write-Host "[start-browser-bridge] python: $python"

# Install bridge deps; do not swallow pip output (BED-189: silent fail left fastapi missing).
Write-Host "[start-browser-bridge] ensuring pip deps from $ReqFile ..."
$pipLog = Join-Path $ScriptsDir ".browser-bridge.pip.log"
& $python -m pip install -r $ReqFile *>&1 | Tee-Object -FilePath $pipLog | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "pip install bridge deps failed (exit $LASTEXITCODE). See $pipLog"
    Write-Warning "Manual: `"$python`" -m pip install -r `"$ReqFile`""
}

# Verify import before Start-Process so ALLSTART gets a clear error, not a dead PID.
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
& $python -c "import fastapi, uvicorn" 2>$null | Out-Null
$importOk = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $prevEap
if (-not $importOk) {
    Write-Warning "Python missing fastapi/uvicorn after pip — bridge will not start. See $pipLog"
    exit 1
}

$LogOut = Join-Path $ScriptsDir ".browser-bridge.out.log"
$LogErr = Join-Path $ScriptsDir ".browser-bridge.err.log"
foreach ($f in @($LogFile, $LogOut, $LogErr)) {
    if (Test-Path -LiteralPath $f) {
        Clear-Content -LiteralPath $f -ErrorAction SilentlyContinue
    }
}

Write-Host "[start-browser-bridge] Starting $BridgePy on :$Port ..."
# Windows Start-Process forbids RedirectStandardOutput and RedirectStandardError
# pointing at the same path — use separate files (same pattern as start-soulcore).
$proc = Start-Process -FilePath $python `
    -ArgumentList @($BridgePy) `
    -WorkingDirectory (Split-Path -Parent $BridgePy) `
    -WindowStyle Hidden `
    -RedirectStandardOutput $LogOut `
    -RedirectStandardError $LogErr `
    -PassThru

$proc.Id | Set-Content -LiteralPath $PidFile -Encoding ascii

$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 250
    if ($proc.HasExited) {
        $err = if (Test-Path -LiteralPath $LogErr) { Get-Content -LiteralPath $LogErr -Raw } else { "" }
        Write-Warning "browser bridge exited early (code $($proc.ExitCode)). See $LogErr / $LogOut"
        if ($err) { Write-Warning $err.Trim() }
        exit 1
    }
    if (Test-BridgeHealthy) {
        $ready = $true
        break
    }
}

if ($ready) {
    Write-Host "[start-browser-bridge] Healthy: $HealthUrl"
    Write-Host "[start-browser-bridge] Extension: chrome://extensions -> Load unpacked -> $(Join-Path $RepoRoot 'BrowserCaptureExtension')"
    exit 0
}

Write-Warning "Bridge process started (PID $($proc.Id)) but /health not confirmed yet. Logs: $LogOut $LogErr"
exit 0
