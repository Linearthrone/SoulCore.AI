# Clear host log for a clean trace, then E1 will run separately.
$logPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
if (Test-Path $logPath) {
    Clear-Content $logPath
    Write-Output 'Host log cleared.'
} else {
    Write-Output 'Log not found (nothing to clear).'
}
