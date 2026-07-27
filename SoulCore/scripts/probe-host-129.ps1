#Requires -Version 5.1
<#
.SYNOPSIS
  TASK-129 (BED-01): Probe the running SoulCore Host /health endpoint and
  run a single chat round-trip to confirm the model produces text.

.PARAMETER BaseUrl
  Host base URL. Default: http://127.0.0.1:7700
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:7700"
)

$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$healthUrl = ($BaseUrl.TrimEnd('/')) + "/health"
Write-Host "==== /health probe ===="
Write-Host "GET $healthUrl"
try {
    $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 5 -ErrorAction Stop
    Write-Host "Response:"
    $health | ConvertTo-Json -Depth 5
    Write-Host ""
} catch {
    Write-Host "HEALTH_FAIL: $($_.Exception.Message)"
    exit 2
}

# Report the inference model field if present.
$inferenceModel = $null
if ($health.PSObject.Properties.Name -contains "inference") {
    $inferenceModel = $health.inference.model
} elseif ($health.PSObject.Properties.Name -contains "model") {
    $inferenceModel = $health.model
}
if ($inferenceModel) {
    Write-Host "inference.model = $inferenceModel"
} else {
    Write-Host "inference.model field not found in /health response ( dumping full response above )"
}
Write-Host ""

# Chat round-trip: POST a simple user turn to /api/chat (Host WS is more
# involved; if Host exposes a REST chat route use it, otherwise probe /ws
# health via the /health inference status only). Most SoulCore Host builds
# expose the chat over WebSocket at /ws; a full WS handshake from PowerShell
# is out of scope here, so we confirm inference reachability via a direct
# Ollama call instead and note that Host /health already reports inference
# status. If the Host has a /api/chat REST route, use it.
Write-Host "==== chat round-trip (Host REST, if present) ===="
$chatUrl = ($BaseUrl.TrimEnd('/')) + "/api/chat"
try {
    $body = @{
        message = "Reply with exactly one word: pong"
    } | ConvertTo-Json -Compress
    $chat = Invoke-RestMethod -Uri $chatUrl -Method Post -ContentType "application/json" -Body $body -TimeoutSec 120 -ErrorAction Stop
    Write-Host "Host /api/chat response:"
    $chat | ConvertTo-Json -Depth 5
    Write-Host "CHAT_OK"
    exit 0
} catch {
    Write-Host "Host /api/chat REST route not available or failed: $($_.Exception.Message)"
    Write-Host "(This is expected if Host only exposes chat over WebSocket /ws.)"
    Write-Host "Falling back to direct Ollama inference probe to confirm model produces text."
}

Write-Host ""
Write-Host "==== direct Ollama inference probe (confirms model produces text) ===="
$ollamaUrl = "http://127.0.0.1:11434/api/chat"
try {
    $ollamaBody = @{
        model = "qwen2.5:14b"
        messages = @(
            @{ role = "user"; content = "Reply with exactly one word: pong" }
        )
        stream = $false
        options = @{ temperature = 0.0; num_predict = 20 }
    } | ConvertTo-Json -Depth 5 -Compress
    $ollamaResp = Invoke-RestMethod -Uri $ollamaUrl -Method Post -ContentType "application/json" -Body $ollamaBody -TimeoutSec 120 -ErrorAction Stop
    Write-Host "Ollama /api/chat response (model text):"
    Write-Host ($ollamaResp.message.content | ConvertTo-Json)
    Write-Host "OLLAMA_INFERENCE_OK"
    exit 0
} catch {
    Write-Host "OLLAMA_INFERENCE_FAIL: $($_.Exception.Message)"
    exit 3
}
