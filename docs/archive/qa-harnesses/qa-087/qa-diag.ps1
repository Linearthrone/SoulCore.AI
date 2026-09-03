Write-Output '=== Check Host process PID 54256 ==='
$proc = Get-Process -Id 54256 -ErrorAction SilentlyContinue
if ($proc) {
    Write-Output ('PID 54256: ' + $proc.ProcessName + ' | StartTime: ' + $proc.StartTime + ' | WS(MB): ' + [math]::Round($proc.WorkingSet64/1MB,1))
} else {
    Write-Output 'PID 54256 NOT FOUND'
}

Write-Output ''
Write-Output '=== All dotnet processes ==='
Get-Process dotnet -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime, @{N='WS_MB';E={[math]::Round($_.WorkingSet64/1MB,1)}} | Format-Table -AutoSize | Out-String | Write-Output

Write-Output '=== Check what listens on 7700 / 11434 / 8642 ==='
$conns = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in 7700,11434,8642,8888 }
foreach ($c in $conns) {
    $p = Get-Process -Id $c.OwningProcess -ErrorAction SilentlyContinue
    Write-Output ('Port ' + $c.LocalPort + ' -> PID ' + $c.OwningProcess + ' (' + $(if($p){$p.ProcessName}else{'?'}) + ')')
}

Write-Output ''
Write-Output '=== Ollama direct re-test (Say hello, num_predict 50) ==='
$body = @{ model='hf.co/UnfilteredAI/NSFW-flash:Q4_K_M'; prompt='Say hello'; stream=$false; options=@{ num_predict=50 } } | ConvertTo-Json -Depth 5
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 70
    $sw.Stop()
    Write-Output ('HTTP_STATUS: ' + $r.StatusCode)
    Write-Output ('ELAPSED_MS: ' + $sw.ElapsedMilliseconds)
    $j = $r.Content | ConvertFrom-Json
    Write-Output ('RESPONSE: ' + $j.response)
    Write-Output ('DONE: ' + $j.done)
} catch {
    $sw.Stop()
    Write-Output ('ELAPSED_MS: ' + $sw.ElapsedMilliseconds)
    Write-Output ('ERR: ' + $_.Exception.Message)
}
