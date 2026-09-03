Write-Output '=== Ollama direct test (warm check) ==='
$body = @{ model='hf.co/UnfilteredAI/NSFW-flash:Q4_K_M'; prompt='Say hello'; stream=$false; options=@{ num_predict=50 } } | ConvertTo-Json -Depth 5
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 90
    $sw.Stop()
    Write-Output ('HTTP_STATUS: ' + $r.StatusCode)
    Write-Output ('ELAPSED_MS: ' + $sw.ElapsedMilliseconds)
    $j = $r.Content | ConvertFrom-Json
    Write-Output ('RESPONSE: ' + $j.response)
    Write-Output ('DONE: ' + $j.done)
    Write-Output ('EVAL_COUNT: ' + $j.eval_count)
} catch {
    $sw.Stop()
    Write-Output ('ELAPSED_MS: ' + $sw.ElapsedMilliseconds)
    Write-Output ('ERR: ' + $_.Exception.Message)
}

Write-Output ''
Write-Output '=== Ollama /api/ps (running models) ==='
try {
    $ps = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/ps' -TimeoutSec 5
    Write-Output ('Models loaded: ' + $ps.models.Count)
    foreach ($m in $ps.models) {
        Write-Output ('  - ' + $m.name + ' | size: ' + $m.size + ' | expires: ' + $m.expires_at)
    }
} catch {
    Write-Output ('ERR: ' + $_.Exception.Message)
}
