$logPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
if (Test-Path $logPath) {
    $f = Get-Item $logPath
    Write-Output ('SIZE: ' + $f.Length)
    Write-Output ('MODIFIED: ' + $f.LastWriteTime)
    Write-Output '--- FULL LOG ---'
    Get-Content $logPath
} else {
    Write-Output 'LOG_NOT_FOUND'
}
