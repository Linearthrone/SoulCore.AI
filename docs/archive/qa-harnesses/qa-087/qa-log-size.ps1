$logPath = 'c:\Users\operator\Soul_Core\SoulCore\scripts\.soulcore-host.log'
$f = Get-Item $logPath
Write-Output ('BEFORE_SIZE: ' + $f.Length)
