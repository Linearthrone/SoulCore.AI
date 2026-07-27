#Requires -Version 5.1
<#
.SYNOPSIS
  TASK-131 (BED-01): Live smoke for the recall_memory + store_memory tools.
  Sends a chat that explicitly asks the model to use recall_memory, then
  inspects the host log for tool dispatch evidence.
#>
[CmdletBinding()]
param(
    [string]$WsUrl = "ws://127.0.0.1:7700/ws",
    [string]$LogPath = "C:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

Write-Host "Connecting to $WsUrl ..."
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ct = New-Object System.Threading.CancellationTokenSource
$ct.CancelAfter([TimeSpan]::FromSeconds(120))
try {
    $connectTask = $ws.ConnectAsync([Uri]$WsUrl, $ct.Token)
    while (-not $connectTask.IsCompleted) { Start-Sleep -Milliseconds 50 }
    if ($connectTask.IsFaulted) { throw $connectTask.Exception }
    Write-Host "Connected. State: $($ws.State)"
} catch {
    Write-Host "WS_CONNECT_FAIL: $($_.Exception.Message)"
    exit 2
}

# Prompt the model to use the recall_memory tool.
$requestObj = @{
    v = 1
    type = "chat.send"
    id = [Guid]::NewGuid().ToString("N")
    ts = ([DateTimeOffset]::UtcNow).ToString("O")
    payload = @{
        text = "Use your recall_memory tool to search for the query 'QUOKKA' with limit 2, then tell me what you found."
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

$ct.CancelAfter([TimeSpan]::FromSeconds(120))
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
    if ($frameText -match "chat\.done") { $doneSeen = $true }
    if ($result.EndOfMessage -and $doneSeen) { break }
    $elapsed = ((Get-Date) - $startTime).TotalSeconds
    if ($elapsed -gt 110) {
        Write-Host "(timeout guard: 110s elapsed, stopping reads)"
        break
    }
}

Write-Host ""
Write-Host "==== summary ===="
Write-Host "frames received: $frameCount"
Write-Host "chat.done seen: $doneSeen"

# Inspect the host log for tool dispatch evidence.
Write-Host ""
Write-Host "==== host log tail (last 80 lines) ===="
if (Test-Path -LiteralPath $LogPath) {
    Get-Content -LiteralPath $LogPath -Tail 80 | ForEach-Object { Write-Host $_ }
    $logContent = Get-Content -LiteralPath $LogPath -Raw
    $toolDispatch = ($logContent | Select-String -Pattern "recall_memory|store_memory|tool_call|ToolRegistry|Unknown tool" -SimpleMatch)
    Write-Host ""
    Write-Host "==== tool-dispatch evidence in log ===="
    if ($toolDispatch) {
        $toolDispatch | ForEach-Object { Write-Host $_.Line }
        Write-Host "TOOL_DISPATCH_EVIDENCE: yes"
    } else {
        Write-Host "TOOL_DISPATCH_EVIDENCE: none (model may not have emitted a tool_call)"
    }
} else {
    Write-Host "Log file not found: $LogPath"
}

try { $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", $ct.Token) | Out-Null } catch {}
$ws.Dispose()
$ct.Dispose()
exit 0
