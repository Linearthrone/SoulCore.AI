# Start local faster-whisper STT (LLMOD quarry) on 127.0.0.1:8000
param(
    [string]$LlmodRoot = "C:\Users\kurtw\LLMOD\LLMOD-max-master",
    [string]$HostAddr = "127.0.0.1",
    [int]$Port = 8000,
    [string]$PythonExe = ""
)

$ErrorActionPreference = "Stop"
$app = Join-Path $LlmodRoot "STTServer\app.py"
if (-not (Test-Path -LiteralPath $app)) { throw "STT app missing: $app" }

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
    $h = Invoke-RestMethod -Uri "http://${HostAddr}:${Port}/health" -TimeoutSec 2
    Write-Host "STT already up: $($h | ConvertTo-Json -Compress)"
    exit 0
} catch { }

Write-Host "Starting faster-whisper STT on ${HostAddr}:${Port} with $PythonExe ..."
$env:WHISPER_MODEL = if ($env:WHISPER_MODEL) { $env:WHISPER_MODEL } else { "base" }
Start-Process -FilePath $PythonExe -ArgumentList @("-m", "uvicorn", "app:app", "--host", $HostAddr, "--port", "$Port") `
    -WorkingDirectory (Split-Path $app -Parent) -WindowStyle Minimized
# OPS-179: short probe — model load can exceed this; do not block ALLSTART.
Start-Sleep 3
try {
    $h = Invoke-RestMethod -Uri "http://${HostAddr}:${Port}/health" -TimeoutSec 5
    Write-Host "STT healthy: $($h | ConvertTo-Json -Compress)"
} catch {
    Write-Warning "STT started but health not ready yet: $_"
}
