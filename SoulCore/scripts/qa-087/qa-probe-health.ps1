$ErrorActionPreference = 'Stop'
Write-Output '=== QA-087 Step 1: Probe Host /health ==='
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:7700/health' -UseBasicParsing -TimeoutSec 10
    Write-Output ('STATUS: ' + $r.StatusCode)
    Write-Output ('BODY: ' + $r.Content)
} catch {
    Write-Output ('ERR: ' + $_.Exception.Message)
    if ($_.Exception.InnerException) {
        Write-Output ('INNER: ' + $_.Exception.InnerException.Message)
    }
}
