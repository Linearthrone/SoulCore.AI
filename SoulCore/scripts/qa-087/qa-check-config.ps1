$binPath = 'c:\Users\kurtw\Soul_Core\SoulCore\SoulCore.Host\bin\Debug\net8.0\appsettings.json'
Write-Output ('=== bin/Debug appsettings.json ===')
if (Test-Path $binPath) {
    Get-Content $binPath
} else {
    Write-Output 'NOT FOUND'
}

Write-Output ''
Write-Output '=== Probe Hermes :8642 ==='
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8642/health' -UseBasicParsing -TimeoutSec 5
    Write-Output ('HERMES /health STATUS: ' + $r.StatusCode)
    Write-Output ('BODY: ' + $r.Content)
} catch {
    Write-Output ('HERMES /health ERR: ' + $_.Exception.Message)
}

Write-Output ''
Write-Output '=== Probe Hermes :8642 /v1/models ==='
try {
    $r2 = Invoke-WebRequest -Uri 'http://127.0.0.1:8642/v1/models' -UseBasicParsing -TimeoutSec 5
    Write-Output ('HERMES /v1/models STATUS: ' + $r2.StatusCode)
    Write-Output ('BODY: ' + $r2.Content)
} catch {
    Write-Output ('HERMES /v1/models ERR: ' + $_.Exception.Message)
}
