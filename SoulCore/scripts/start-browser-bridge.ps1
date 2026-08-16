#Requires -Version 5.1
<#
.SYNOPSIS
  Start House Victoria browser capture bridge on 127.0.0.1:17891 (hidden, no console).
.DESCRIPTION
  Runs BrowserCaptureBridge\bridge_server.py via pythonw so no terminal window appears.
  Prefers LLMOD MCPServer venv (fastapi/uvicorn), then V:\Python311\pythonw.exe.
#>
[CmdletBinding()]
param(
    [int]$Port = 17891,
    [string]$BindAddress = "127.0.0.1",
    [string]$PythonwExe = "",
    [switch]$ForceRestart
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$SoulCoreRoot = Split-Path -Parent $ScriptsDir
$RepoRoot = Split-Path -Parent $SoulCoreRoot
$BridgePy = Join-Path $RepoRoot "BrowserCaptureBridge\bridge_server.py"
if (-not (Test-Path -LiteralPath $BridgePy)) {
    throw "bridge_server.py not found: $BridgePy"
}

$PidFile = Join-Path $ScriptsDir ".browser-bridge.pid"
$HealthUrl = "http://${BindAddress}:${Port}/health"

function Resolve-BridgePythonw {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path -LiteralPath $Preferred)) { return $Preferred }
    foreach ($c in @(
        "C:\Users\kurtw\LLMOD\LLMOD-max-master\MCPServer\.venv\Scripts\pythonw.exe",
        "V:\Python311\pythonw.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.12-64\pythonw.exe",
        "C:\Users\kurtw\LLMOD\LLMOD-max-master\MCPServer\.venv\Scripts\python.exe",
        "V:\Python311\python.exe"
    )) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    return "pythonw"
}

function Test-BridgeHealth {
    try {
        $null = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 2 -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

function Stop-BridgeOnPort {
    $conns = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -eq $BindAddress -or $_.LocalAddress -eq "0.0.0.0" }
    foreach ($c in @($conns)) {
        Write-Host "Stopping browser bridge PID $($c.OwningProcess) on :$Port"
        Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $PidFile) {
        Remove-Item -LiteralPath $PidFile -Force -ErrorAction SilentlyContinue
    }
}

if (-not $ForceRestart -and (Test-BridgeHealth)) {
    Write-Host "Browser bridge already up: $HealthUrl"
    exit 0
}

if ($ForceRestart) {
    Stop-BridgeOnPort
    Start-Sleep -Milliseconds 400
}

$PythonwExe = Resolve-BridgePythonw $PythonwExe
$workDir = Split-Path -Parent $BridgePy
$usePythonw = $PythonwExe -match '(?i)pythonw\.exe$'

Write-Host "Starting browser bridge on $HealthUrl with $PythonwExe (hidden) ..."

# pythonw has no console handles - do not RedirectStandard* (fails with invalid handle).
# python.exe: Hidden + redirects keeps a console from appearing.
$startArgs = @{
    FilePath         = $PythonwExe
    ArgumentList     = @($BridgePy)
    WorkingDirectory = $workDir
    WindowStyle      = "Hidden"
    PassThru         = $true
}
if (-not $usePythonw) {
    $log = Join-Path $ScriptsDir ".browser-bridge.log"
    $err = Join-Path $ScriptsDir ".browser-bridge.err.log"
    $startArgs.RedirectStandardOutput = $log
    $startArgs.RedirectStandardError = $err
}

$proc = Start-Process @startArgs
if (-not $proc) {
    throw "Failed to start browser bridge process"
}
Set-Content -LiteralPath $PidFile -Value $proc.Id -Encoding ascii

$deadline = [DateTime]::UtcNow.AddSeconds(12)
while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-BridgeHealth) {
        Write-Host "Browser bridge healthy: $HealthUrl (PID $($proc.Id))"
        exit 0
    }
    if ($proc.HasExited) {
        throw "Browser bridge exited early (code $($proc.ExitCode)). Check fastapi/uvicorn in that Python."
    }
    Start-Sleep -Milliseconds 400
}

Write-Warning "Browser bridge started (PID $($proc.Id)) but $HealthUrl not ready yet"
exit 0
