# Check Ollama server log for the Host's requests
$ollamaLogPaths = @(
    "$env:LOCALAPPDATA\Ollama\server.log",
    "$env:USERPROFILE\.ollama\logs\server.log",
    "C:\Users\kurtw\.ollama\logs\server.log",
    "$env:LOCALAPPDATA\Ollama\app.log"
)
foreach ($p in $ollamaLogPaths) {
    if (Test-Path $p) {
        $f = Get-Item $p
        Write-Output ('=== ' + $p + ' (size=' + $f.Length + ', modified=' + $f.LastWriteTime + ') ===')
        Write-Output '--- LAST 60 LINES ---'
        Get-Content $p -Tail 60
        Write-Output ''
    } else {
        Write-Output ('NOT FOUND: ' + $p)
    }
}

# Also check the Ollama /api/ps to confirm model is loaded
Write-Output '=== Ollama /api/ps ==='
try {
    $ps = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/ps' -TimeoutSec 5
    Write-Output ('Models loaded: ' + $ps.models.Count)
    foreach ($m in $ps.models) { Write-Output ('  - ' + $m.name) }
} catch { Write-Output ('ERR: ' + $_.Exception.Message) }
