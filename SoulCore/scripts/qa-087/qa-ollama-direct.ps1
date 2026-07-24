$ErrorActionPreference = 'Stop'
Write-Output '=== QA-087 Step 2: Direct Ollama /api/generate test ==='
$model = 'hf.co/UnfilteredAI/NSFW-flash:Q4_K_M'
$body = @{
    model = $model
    prompt = 'Say hello'
    stream = $false
    options = @{ num_predict = 50 }
} | ConvertTo-Json -Depth 5

Write-Output ('Model: ' + $model)
Write-Output ('Prompt: Say hello')
Write-Output ('num_predict: 50')
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 60
    $sw.Stop()
    Write-Output ('HTTP_STATUS: ' + $r.StatusCode)
    Write-Output ('ELAPSED_MS: ' + $sw.ElapsedMilliseconds)
    $json = $r.Content | ConvertFrom-Json
    Write-Output ('RESPONSE_FIELD: ' + $json.response)
    Write-Output ('DONE: ' + $json.done)
    Write-Output ('EVAL_COUNT: ' + $json.eval_count)
    Write-Output ('TOTAL_DURATION: ' + $json.total_duration)
    Write-Output ('LOAD_DURATION: ' + $json.load_duration)
    Write-Output ('PROMPT_EVAL_DURATION: ' + $json.prompt_eval_duration)
    Write-Output ('EVAL_DURATION: ' + $json.eval_duration)
    Write-Output 'RESULT: PASS'
} catch {
    $sw.Stop()
    Write-Output ('ELAPSED_MS: ' + $sw.ElapsedMilliseconds)
    Write-Output ('ERR: ' + $_.Exception.Message)
    Write-Output 'RESULT: FAIL'
}
