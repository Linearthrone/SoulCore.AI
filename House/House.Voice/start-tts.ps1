# Start local Chatterbox TTS (LLMOD quarry) on 127.0.0.1:8881
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

function Resolve-VoicePython {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path -LiteralPath $Preferred)) { return $Preferred }
    foreach ($c in @(
        "V:\Python311\python.exe",
        "$env:LOCALAPPDATA\Python\pythoncore-3.12-64\python.exe"
    )) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    try {
        $py311 = & py -3.11 -c "import sys; print(sys.executable)" 2>$null
        if ($LASTEXITCODE -eq 0 -and $py311) { return $py311.Trim() }
    } catch {}
    return "python"
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

Write-Host "Starting Chatterbox TTS on ${HostAddr}:${Port} with $PythonExe (voices=$voices device=$($env:CHATTERBOX_DEVICE)) ..."
Start-Process -FilePath $PythonExe -ArgumentList @($app) `
    -WorkingDirectory (Split-Path $app -Parent) -WindowStyle Minimized `
    -RedirectStandardError "$env:TEMP\chatterbox-err.log" -RedirectStandardOutput "$env:TEMP\chatterbox-out.log"
# OPS-179: short probe — CUDA/model load can exceed this; do not block ALLSTART.
Start-Sleep 5
try {
    $r = Invoke-WebRequest -Uri "http://${HostAddr}:${Port}/" -TimeoutSec 5 -UseBasicParsing
    Write-Host "Chatterbox healthy (HTTP $($r.StatusCode))"
} catch {
    Write-Warning "Chatterbox started but not ready yet: $_"
    Write-Warning "See $env:TEMP\chatterbox-err.log — install deps: $PythonExe -m pip install -r requirements.txt"
}
