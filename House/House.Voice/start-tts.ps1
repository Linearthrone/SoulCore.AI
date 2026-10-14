# Start House Victoria TTS (Kokoro, in this repo) on 127.0.0.1:8881.
# Without -Worker this returns immediately and leaves a hidden worker running,
# so ALLSTART's timeout does not kill the model download.
param(
    [string]$HostAddr = "127.0.0.1",
    [int]$Port = 8881,
    [string]$PythonExe = "",
    [switch]$Worker
)

$ErrorActionPreference = "Stop"
$Server = Join-Path $PSScriptRoot "tts\server.py"
$Requirements = Join-Path $PSScriptRoot "tts\requirements.txt"
if (-not (Test-Path -LiteralPath $Server)) { throw "TTS server missing: $Server" }

if (-not $Worker) {
    try {
        $r = Invoke-WebRequest -Uri "http://${HostAddr}:${Port}/health" -TimeoutSec 2 -UseBasicParsing
        Write-Host "TTS already up (HTTP $($r.StatusCode))"
        exit 0
    } catch { }
    $arg = @(
        "-NoProfile", "-ExecutionPolicy", "Bypass",
        "-File", $PSCommandPath,
        "-Worker",
        "-HostAddr", $HostAddr,
        "-Port", "$Port"
    )
    if ($PythonExe) { $arg += @("-PythonExe", $PythonExe) }
    Start-Process -FilePath "powershell.exe" -ArgumentList $arg -WindowStyle Hidden | Out-Null
    Write-Host "TTS worker started. First run downloads the Kokoro model into House\House.Voice\models."
    Write-Host "Logs: $env:TEMP\house-tts-err.log"
    exit 0
}

function Resolve-VoicePython {
    param([string]$Preferred)
    if ($Preferred -and (Test-Path -LiteralPath $Preferred)) { return $Preferred }
    foreach ($c in @(
        "V:\Python311\python.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe",
        "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe"
    )) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    return "python"
}

$PythonExe = Resolve-VoicePython $PythonExe
$env:HOUSE_TTS_HOST = $HostAddr
$env:HOUSE_TTS_PORT = "$Port"
$outLog = Join-Path $env:TEMP "house-tts-out.log"
$errLog = Join-Path $env:TEMP "house-tts-err.log"

& $PythonExe -c "import kokoro_onnx" 2>$null
if ($LASTEXITCODE -ne 0) {
    & $PythonExe -m pip install -r $Requirements
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Start-Process -FilePath $PythonExe -ArgumentList @($Server) `
    -WorkingDirectory (Split-Path $Server -Parent) `
    -WindowStyle Hidden `
    -RedirectStandardError $errLog `
    -RedirectStandardOutput $outLog | Out-Null
