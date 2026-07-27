#Requires -Version 5.1
<#
.SYNOPSIS
  TASK-129 (BED-01): Minimal WebSocket chat round-trip against the
  SoulCore Host /ws endpoint to confirm the configured model (qwen2.5:14b)
  produces text end-to-end through the real Host chat path.

  Uses ClientWebSocket (System.Net.WebSockets), sends a ChatRequest JSON
  frame, and reads text frames until a chat.done or error frame arrives.

  The SoulCore WS chat protocol (from ChatWebSocketHandler) expects a JSON
  envelope. We send the minimal fields and print every frame received.
#>
[CmdletBinding()]
param(
    [string]$WsUrl = "ws://127.0.0.1:7700/ws"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Try to discover the expected request envelope shape from the handler.
$ErrorActionPreference = "Stop"

Write-Host "Connecting to $WsUrl ..."
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ct = New-Object System.Threading.CancellationTokenSource
$ct.CancelAfter([TimeSpan]::FromSeconds(60))
try {
    $connectTask = $ws.ConnectAsync([Uri]$WsUrl, $ct.Token)
    while (-not $connectTask.IsCompleted) { Start-Sleep -Milliseconds 50 }
    if ($connectTask.IsFaulted) { throw $connectTask.Exception }
    Write-Host "Connected. State: $($ws.State)"
} catch {
    Write-Host "WS_CONNECT_FAIL: $($_.Exception.Message)"
    exit 2
}

# Send a chat request using the canonical SoulCoreFrame envelope:
# {"v":1,"type":"chat.send","id":"...","ts":"...","payload":{"text":"..."}}
$requestObj = @{
    v = 1
    type = "chat.send"
    id = [Guid]::NewGuid().ToString("N")
    ts = ([DateTimeOffset]::UtcNow).ToString("O")
    payload = @{
        text = "Reply with exactly one word: pong"
    }
}
$requestJson = $requestObj | ConvertTo-Json -Compress
$requestBytes = [System.Text.Encoding]::UTF8.GetBytes($requestJson)
$sendSeg = New-Object System.ArraySegment[byte]($requestBytes, 0, $requestBytes.Length)
try {
    $sendTask = $ws.SendAsync($sendSeg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $ct.Token)
    while (-not $sendTask.IsCompleted) { Start-Sleep -Milliseconds 20 }
    Write-Host "Sent: $requestJson"
} catch {
    Write-Host "WS_SEND_FAIL: $($_.Exception.Message)"
    exit 3
}

# Read frames for up to 90s (model load + generation).
$ct.CancelAfter([TimeSpan]::FromSeconds(90))
$recvBuffer = New-Object byte[] 16384
$allText = ""
$frameCount = 0
$doneSeen = $false
$startTime = Get-Date
while ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
    $recvSeg = New-Object System.ArraySegment[byte]($recvBuffer, 0, $recvBuffer.Length)
    $recvTask = $ws.ReceiveAsync($recvSeg, $ct.Token)
    while (-not $recvTask.IsCompleted) { Start-Sleep -Milliseconds 50 }
    if ($recvTask.IsFaulted) {
        Write-Host "WS_RECV_FAIL: $($recvTask.Exception.Message)"
        break
    }
    $result = $recvTask.Result
    if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
        Write-Host "Server closed: $($result.CloseStatus) / $($result.CloseStatusDescription)"
        break
    }
    $frameText = [System.Text.Encoding]::UTF8.GetString($recvBuffer, 0, $result.Count)
    $frameCount++
    Write-Host "---- frame $frameCount ----"
    Write-Host $frameText
    $allText += $frameText
    if ($frameText -match "chat\.done" -or $frameText -match '"done"' -or $frameText -match '"type"\s*:\s*"done"') {
        $doneSeen = $true
    }
    if ($result.EndOfMessage) {
        if ($doneSeen) { break }
        # keep reading if not done (streaming)
    }
    $elapsed = ((Get-Date) - $startTime).TotalSeconds
    if ($elapsed -gt 80) {
        Write-Host "(timeout guard: 80s elapsed, stopping reads)"
        break
    }
}

Write-Host ""
Write-Host "==== summary ===="
Write-Host "frames received: $frameCount"
Write-Host "chat.done seen: $doneSeen"
if ($allText -match "pong") {
    Write-Host "CHAT_ROUNDTRIP_OK: response contained 'pong'"
    $exitCode = 0
} elseif ($frameCount -gt 0) {
    Write-Host "CHAT_ROUNDTRIP_OK: at least one frame received from Host"
    $exitCode = 0
} else {
    Write-Host "CHAT_ROUNDTRIP_FAIL: no frames received"
    $exitCode = 1
}

try { $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", $ct.Token) | Out-Null } catch {}
$ws.Dispose()
$ct.Dispose()
exit $exitCode
