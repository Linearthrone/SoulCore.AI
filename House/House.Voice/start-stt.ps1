# Start local faster-whisper STT (LLMOD quarry) on 127.0.0.1:8000
# Prefer pythonw.exe so ALLSTART does not leave blank console windows (OPS-178 pattern).
param(
    [string]$LlmodRoot = "C:\Users\kurtw\LLMOD\LLMOD-max-master",
    [string]$HostAddr = "127.0.0.1",
    [int]$Port = 8000,
    [string]$PythonExe = ""
)

$ErrorActionPreference = "Stop"
$app = Join-Path $LlmodRoot "STTServer\app.py"
if (-not (Test-Path -LiteralPath $app)) { throw "STT app missing: $app" }

function Prefer-Pythonw {
    param([Parameter(Mandatory = $true)][string]$Exe)
    if ($Exe -match '(?i)(^|[\\/])pythonw\.exe$') { return $Exe }
    if ($Exe -match '(?i)python\.exe$') {
        $w = $Exe -replace '(?i)python\.exe$', 'pythonw.exe'
        if (Test-Path -LiteralPath $w) { return $w }
        Write-Warning "pythonw.exe sibling missing next to $Exe - a console window may appear"
        return $Exe
    }
    # Bare "python" on PATH — try sibling pythonw via where.exe
    try {
        $where = & where.exe pythonw 2>$null | Select-Object -First 1
        if ($where -and (Test-Path -LiteralPath $where)) { return $where.Trim() }
    } catch { }
    return $Exe
}

function Resolve-VoicePython {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path -LiteralPath $Preferred)) {
        return (Prefer-Pythonw $Preferred)
    }
    foreach ($c in @(
        "V:\Python311\pythonw.exe",
        "V:\Python311\python.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.12-64\pythonw.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.12-64\python.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python312\pythonw.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python311\pythonw.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe"
    )) {
        if (Test-Path -LiteralPath $c) { return (Prefer-Pythonw $c) }
    }
    try {
        $py311 = & py -3.11 -c "import sys; print(sys.executable)" 2>$null
        if ($LASTEXITCODE -eq 0 -and $py311) {
            return (Prefer-Pythonw $py311.Trim())
        }
    } catch {}
    return (Prefer-Pythonw "python")
}

$PythonExe = Resolve-VoicePython $PythonExe

try {
    $h = Invoke-RestMethod -Uri "http://${HostAddr}:${Port}/health" -TimeoutSec 2
    Write-Host "STT already up: $($h | ConvertTo-Json -Compress)"
    exit 0
} catch { }

$outLog = Join-Path $env:TEMP "soulcore-stt-out.log"
$errLog = Join-Path $env:TEMP "soulcore-stt-err.log"
foreach ($f in @($outLog, $errLog)) {
    if (Test-Path -LiteralPath $f) {
        Clear-Content -LiteralPath $f -ErrorAction SilentlyContinue
    }
}

Write-Host "Starting faster-whisper STT on ${HostAddr}:${Port} with $PythonExe (hidden) ..."
$env:WHISPER_MODEL = if ($env:WHISPER_MODEL) { $env:WHISPER_MODEL } else { "base" }
# Hidden + redirects + pythonw: no blank console on the desktop (Minimized + python.exe left two windows).
Start-Process -FilePath $PythonExe `
    -ArgumentList @("-m", "uvicorn", "app:app", "--host", $HostAddr, "--port", "$Port") `
    -WorkingDirectory (Split-Path $app -Parent) `
    -WindowStyle Hidden `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog `
    -PassThru | Out-Null
# OPS-179: short probe - model load can exceed this; do not block ALLSTART.
Start-Sleep 3
try {
    $h = Invoke-RestMethod -Uri "http://${HostAddr}:${Port}/health" -TimeoutSec 5
    Write-Host "STT healthy: $($h | ConvertTo-Json -Compress)"
} catch {
    Write-Warning "STT started but health not ready yet: $_"
    Write-Warning "See $errLog / $outLog"
}
