$db = Join-Path $env:LOCALAPPDATA 'SoulCore\memory\soulcore_memory.db'
$sql = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.qa096-query-anchors.sql'
Write-Output "DB: $db"
sqlite3 $db ".read $sql"
