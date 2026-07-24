$ErrorActionPreference = 'Stop'
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:7700/health' -TimeoutSec 10 -UseBasicParsing
    Write-Output ("STATUS: " + $r.StatusCode)
    Write-Output ("BODY: " + $r.Content)
} catch {
    Write-Output ("ERROR: " + $_.Exception.Message)
}
