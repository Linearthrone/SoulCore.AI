# qa086-ollama-direct.ps1
# Direct Ollama backend probe — test NSFW-flash:Q4_K_M with num_predict=256
# to confirm the model generates a response within 180s (validates the BED-085 fix
# would work IF the correct config were deployed).
$ErrorActionPreference = 'Stop'

$OllamaUrl = 'http://127.0.0.1:11434'
$Model = 'hf.co/UnfilteredAI/NSFW-flash:Q4_K_M'
$Prompt = 'Say hello'
$NumPredict = 256
$TimeoutSec = 180

Write-Output '====== Direct Ollama probe (NSFW-flash:Q4_K_M, num_predict=256) ======'
Write-Output ("Model: " + $Model)
Write-Output ("Prompt: " + $Prompt)
Write-Output ("num_predict: " + $NumPredict)
Write-Output ("Timeout: " + $TimeoutSec + "s")

# 1. Check Ollama is up + list models
try {
    $tags = Invoke-RestMethod -Uri "$OllamaUrl/api/tags" -TimeoutSec 10
    Write-Output ("Ollama /api/tags OK; models loaded: " + $tags.models.Count)
    foreach ($m in $tags.models) {
        Write-Output ("  - " + $m.name + " (" + [math]::Round($m.size/1GB,2) + " GB)")
    }
    $hasFlash = $false
    foreach ($m in $tags.models) { if ($m.name -like '*NSFW-flash*') { $hasFlash = $true } }
    Write-Output ("NSFW-flash model present: " + $hasFlash)
} catch {
    Write-Output ("Ollama /api/tags FAILED: " + $_.Exception.Message)
    exit 1
}

# 2. Generate with num_predict=256
$body = @{
    model = $Model
    prompt = $Prompt
    stream = $false
    options = @{ num_predict = $NumPredict }
} | ConvertTo-Json -Compress

Write-Output ''
Write-Output ("Sending POST /api/generate ...")
$start = [DateTime]::UtcNow
try {
    $resp = Invoke-WebRequest -Uri "$OllamaUrl/api/generate" -Method POST -Body $body -ContentType 'application/json' -TimeoutSec $TimeoutSec -UseBasicParsing
    $elapsed = [int]([DateTime]::UtcNow - $start).TotalSeconds
    Write-Output ("Generate OK in " + $elapsed + "s")
    Write-Output ("Status: " + $resp.StatusCode)
    $json = $resp.Content | ConvertFrom-Json
    Write-Output ("Response text: " + $json.response)
    Write-Output ("eval_count: " + $json.eval_count)
    Write-Output ("eval_duration_s: " + ($json.eval_duration / 1e9))
    Write-Output ("total_duration_s: " + ($json.total_duration / 1e9))
    Write-Output ''
    Write-Output '===== DIRECT PROBE RESULT ====='
    Write-Output 'Result: Pass (NSFW-flash generates within timeout with num_predict=256)'
    Write-Output ("Elapsed: " + $elapsed + "s")
} catch {
    $elapsed = [int]([DateTime]::UtcNow - $start).TotalSeconds
    Write-Output ("Generate FAILED after " + $elapsed + "s: " + $_.Exception.Message)
    Write-Output ''
    Write-Output '===== DIRECT PROBE RESULT ====='
    Write-Output 'Result: Fail'
    Write-Output ("Elapsed: " + $elapsed + "s")
    Write-Output ("Error: " + $_.Exception.Message)
}
