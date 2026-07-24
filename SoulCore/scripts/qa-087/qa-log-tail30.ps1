$logPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
Write-Output '--- LAST 30 LINES ---'
Get-Content $logPath -Tail 30
