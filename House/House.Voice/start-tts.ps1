# Start local Chatterbox TTS (LLMOD quarry) on 127.0.0.1:8881
# Prefer pythonw.exe so ALLSTART does not leave blank console windows (OPS-178 pattern).
param(
    [string]$LlmodRoot = "C:\Users\kurtw\LLMOD\LLMOD-max-master",
    [string]$HostAddr = "127.0.0.1",
    [int]$Port = 8881,
    [string]$PythonExe = "",
    [string]$Device = "cuda"
)

$ErrorActionPreference = "Stop"
$app = Join-Path $LlmodRoot "ChatterboxServer\chatterbox_server.py"
if (-not (Test-Path -LiteralPath $app)) { throw "Chatterbox server missing: $app" }

function Prefer-Pythonw {
    param([Parameter(Mandatory = $true)][string]$Exe)
    if ($Exe -match '(?i)(^|[\\/])pythonw\.exe$') { return $Exe }
    if ($Exe -match '(?i)python\.exe$') {
        $w = $Exe -replace '(?i)python\.exe$', 'pythonw.exe'
        if (Test-Path -LiteralPath $w) { return $w }
        Write-Warning "pythonw.exe sibling missing next to $Exe - a console window may appear"
        return $Exe
    }
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
    $r = Invoke-WebRequest -Uri "http://${HostAddr}:${Port}/" -TimeoutSec 2 -UseBasicParsing
    Write-Host "Chatterbox already up (HTTP $($r.StatusCode))"
    exit 0
} catch { }

$voices = Join-Path $LlmodRoot "Media\ChatterboxVoices"
$env:CHATTERBOX_HOST = $HostAddr
$env:CHATTERBOX_PORT = "$Port"
$env:CHATTERBOX_VOICES_DIR = $voices
$env:CHATTERBOX_DEVICE = if ($env:CHATTERBOX_DEVICE) { $env:CHATTERBOX_DEVICE } else { $Device }

$outLog = Join-Path $env:TEMP "chatterbox-out.log"
$errLog = Join-Path $env:TEMP "chatterbox-err.log"
foreach ($f in @($outLog, $errLog)) {
    if (Test-Path -LiteralPath $f) {
        Clear-Content -LiteralPath $f -ErrorAction SilentlyContinue
    }
}

Write-Host "Starting Chatterbox TTS on ${HostAddr}:${Port} with $PythonExe (hidden; voices=$voices device=$($env:CHATTERBOX_DEVICE)) ..."
# Hidden + redirects + pythonw: no blank console on the desktop.
Start-Process -FilePath $PythonExe -ArgumentList @($app) `
    -WorkingDirectory (Split-Path $app -Parent) `
    -WindowStyle Hidden `
    -RedirectStandardError $errLog `
    -RedirectStandardOutput $outLog `
    -PassThru | Out-Null
# OPS-179: short probe - CUDA/model load can exceed this; do not block ALLSTART.
Start-Sleep 5
try {
    $r = Invoke-WebRequest -Uri "http://${HostAddr}:${Port}/" -TimeoutSec 5 -UseBasicParsing
    Write-Host "Chatterbox healthy (HTTP $($r.StatusCode))"
} catch {
    Write-Warning "Chatterbox started but not ready yet: $_"
    Write-Warning ("See {0} - install deps: {1} -m pip install -r requirements.txt" -f $errLog, $PythonExe)
}
