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
$LogOut = Join-Path $ScriptsDir ".browser-bridge.out.log"
$LogErr = Join-Path $ScriptsDir ".browser-bridge.err.log"
$PipLog = Join-Path $ScriptsDir ".browser-bridge.pip.log"
$HealthUrl = "http://127.0.0.1:${Port}/health"

if (-not (Test-Path -LiteralPath $BridgePy)) {
    throw "Missing bridge: $BridgePy"
}

function Test-BridgeHealthy {
    try {
        $r = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 2 -ErrorAction Stop
        return ($null -ne $r -and $r.ok -eq $true)
    } catch {
        return $false
    }
}

function Prefer-Pythonw {
    param([Parameter(Mandatory = $true)][string]$Exe)
    # OPS-178 pattern: pythonw.exe has no console subsystem (blank windows gone).
    if ($Exe -match '(?i)(^|[\\/])pythonw\.exe$') { return $Exe }
    if ($Exe -match '(?i)python\.exe$') {
        $w = $Exe -replace '(?i)python\.exe$', 'pythonw.exe'
        if (Test-Path -LiteralPath $w) { return $w }
    }
    return $Exe
}

function Resolve-Python {
    # Prefer a normal Python install — never the old Hermes agent venv.
    # Returns console python.exe (for pip / -c). Long-running bridge uses Prefer-Pythonw.
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

if ((-not $ForceRestart) -and (Test-BridgeHealthy)) {
    Write-Host "[start-browser-bridge] Already healthy at $HealthUrl"
    exit 0
}

if (Test-Path -LiteralPath $PidFile) {
    try {
        $old = [int](Get-Content -LiteralPath $PidFile -Raw).Trim()
        if ($old -gt 0) {
            if ($ForceRestart -or -not (Test-BridgeHealthy)) {
                Stop-Process -Id $old -Force -ErrorAction SilentlyContinue
            }
        }
    } catch { }
}

$python = Resolve-Python
if (-not $python) {
    throw "python.exe not found - install Python 3.11+ or ensure it is on PATH (not Hermes venv)"
}
# pip / import checks need console python; the long-running bridge uses pythonw when present.
$pythonRun = Prefer-Pythonw $python

Write-Host "[start-browser-bridge] python: $python"
if ($pythonRun -ne $python) {
    Write-Host "[start-browser-bridge] bridge runtime (no console): $pythonRun"
}
Write-Host "[start-browser-bridge] ensuring pip deps from $ReqFile ..."
$PipErr = "$PipLog.err"
if (Test-Path -LiteralPath $PipLog) { Remove-Item -LiteralPath $PipLog -Force -ErrorAction SilentlyContinue }
if (Test-Path -LiteralPath $PipErr) { Remove-Item -LiteralPath $PipErr -Force -ErrorAction SilentlyContinue }
$pipProc = Start-Process -FilePath $python `
    -ArgumentList @("-m", "pip", "install", "-r", $ReqFile) `
    -NoNewWindow `
    -Wait `
    -PassThru `
    -RedirectStandardOutput $PipLog `
    -RedirectStandardError $PipErr
$pipExit = $pipProc.ExitCode
if (Test-Path -LiteralPath $PipErr) {
    Get-Content -LiteralPath $PipErr -Encoding utf8 -ErrorAction SilentlyContinue |
        Add-Content -LiteralPath $PipLog -Encoding utf8
}
if ($pipExit -ne 0) {
    Write-Warning "pip install bridge deps failed (exit $pipExit). See $PipLog"
    Write-Warning ('Manual: "{0}" -m pip install -r "{1}"' -f $python, $ReqFile)
}

& $python -c "import fastapi, uvicorn" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Warning ("Python missing fastapi/uvicorn after pip - bridge will not start. See {0}" -f $PipLog)
    exit 1
}

foreach ($f in @($LogOut, $LogErr)) {
    if (Test-Path -LiteralPath $f) {
        Clear-Content -LiteralPath $f -ErrorAction SilentlyContinue
    }
}

Write-Host "[start-browser-bridge] Starting $BridgePy on :$Port ..."
$proc = Start-Process -FilePath $pythonRun `
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
    $extPath = Join-Path $RepoRoot "BrowserCaptureExtension"
    Write-Host ("[start-browser-bridge] Extension: chrome://extensions -> Load unpacked -> {0}" -f $extPath)
    exit 0
}

Write-Warning ("Bridge process started (PID {0}) but /health not confirmed yet. Logs: {1} {2}" -f $proc.Id, $LogOut, $LogErr)
exit 0
