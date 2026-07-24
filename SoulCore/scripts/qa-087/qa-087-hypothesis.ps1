# Hypothesis test: client ReceiveAsync cancel aborts the WS connection, causing RequestAborted to fire.
# This version sends chat.send then does NOT call ReceiveAsync for 10s (lets inference complete),
# THEN reads all accumulated frames.
$ErrorActionPreference = 'Stop'

$HostWsUrl = 'ws://127.0.0.1:7700/ws'
$UnrealWsUrl = 'ws://127.0.0.1:8888'

function Connect-WsR { param([string]$Url, [int]$TimeoutMs = 8000); $ws = [System.Net.WebSockets.ClientWebSocket]::new(); $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs); $t = $ws.ConnectAsync([Uri]$Url, $cts.Token); if (-not $t.Wait($TimeoutMs)) { $cts.Cancel(); $ws.Dispose(); throw "timeout" }; $cts.Dispose(); return $ws }
function Send-FrameR { param($Ws, [string]$Json); $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json); $seg = [ArraySegment[byte]]::new($bytes); $cts = [System.Threading.CancellationTokenSource]::new(5000); $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(5000) | Out-Null; $cts.Dispose() }
function New-FrameR { param([string]$Type, $Payload); $id = [Guid]::NewGuid().ToString('N'); $ts = [DateTimeOffset]::UtcNow.ToString('O'); return @{ v = 1; type = $Type; id = $id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10 }
function Short-S { param([string]$S, [int]$Len = 200); $s2 = ($S -replace '\s+',' '); return $s2.Substring(0, [Math]::Min($Len, $s2.Length)) }
function Close-WsR { param($Ws); if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) { try { $c=[System.Threading.CancellationTokenSource]::new(2000); $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c.Token).Wait(2000); $c.Dispose() } catch { } }; if ($Ws) { $Ws.Dispose() } }

# Non-canceling receive: use a large timeout so we never cancel an in-flight receive.
function Receive-FrameSafe { param($Ws, [int]$WaitMs = 15000)
    $buf = New-Object byte[] 32768
    $seg = [ArraySegment[byte]]::new($buf)
    $cts = [System.Threading.CancellationTokenSource]::new($WaitMs)
    $task = $Ws.ReceiveAsync($seg, $cts.Token)
    if ($task.Wait($WaitMs)) {
        $cts.Dispose()
        $count = $task.Result.Count
        if ($count -eq 0) { return $null }
        return [System.Text.Encoding]::UTF8.GetString($buf, 0, $count)
    } else {
        # DON'T cancel - just dispose the CTS and return null
        # Actually we must cancel to free the task, but do it AFTER checking state
        $cts.Cancel()
        $cts.Dispose()
        return $null
    }
}

Write-Output '=== Hypothesis test: send chat.send, wait 10s WITHOUT receiving, then read ==='

# Warm Ollama first
$body = @{ model='hf.co/UnfilteredAI/NSFW-flash:Q4_K_M'; prompt='hi'; stream=$false; options=@{ num_predict=10 } } | ConvertTo-Json -Depth 5
try { Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 60 | Out-Null; Write-Output 'Ollama warmed.' } catch { Write-Output ('Warm ERR: ' + $_.Exception.Message) }

# UE listener
$ueWs = $null
try { $ueWs = Connect-WsR -Url $UnrealWsUrl -TimeoutMs 5000; Write-Output 'UE connected.' } catch { Write-Output ('UE: ' + $_.Exception.Message) }

# Host WS
$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

# Send chat.send
$frame = New-FrameR -Type 'chat.send' -Payload @{ text = 'Say hello'; sessionId = 'qa087-hypo' }
Write-Output ('send: ' + $frame)
Send-FrameR -Ws $hostWs -Json $frame
$sendTime = [DateTime]::UtcNow
Write-Output ('send-done: ' + $sendTime.ToString('O'))

# KEY: Do NOT receive for 12 seconds - let inference complete without canceling any ReceiveAsync
Write-Output 'Waiting 12s without receiving (let inference run)...'
Start-Sleep -Seconds 12

# Now read all accumulated frames with a generous timeout
Write-Output 'Reading frames now...'
$hostFrames = @()
$ueFrames = @()
$readDeadline = [DateTime]::UtcNow.AddSeconds(15)
while ([DateTime]::UtcNow -lt $readDeadline) {
    $r = Receive-FrameSafe -Ws $hostWs -WaitMs 3000
    if ($r) { $hostFrames += $r; Write-Output ('host[' + $hostFrames.Count + ']: ' + (Short-S $r)) }
    else { break }
    if ($r -match '"type"\s*:\s*"chat\.done"') { Write-Output 'chat.done SEEN!'; break }
}
if ($ueWs) {
    $ueDeadline = [DateTime]::UtcNow.AddSeconds(5)
    while ([DateTime]::UtcNow -lt $ueDeadline) {
        $f = Receive-FrameSafe -Ws $ueWs -WaitMs 2000
        if ($f) { $ueFrames += $f; Write-Output ('ue[' + $ueFrames.Count + ']: ' + (Short-S $f)) }
        else { break }
    }
}

Close-WsR $hostWs
Close-WsR $ueWs

$chatDone = $hostFrames | Where-Object { $_ -match 'chat\.done' } | Select-Object -First 1
Write-Output ''
Write-Output '===== HYPOTHESIS RESULT ====='
Write-Output ('hostFrames: ' + $hostFrames.Count)
Write-Output ('chatDone: ' + [bool]$chatDone)
if ($chatDone) {
    Write-Output 'RESULT: PASS - inference completed when client did NOT cancel ReceiveAsync during inference'
} else {
    Write-Output 'RESULT: FAIL - inference still timed out even without ReceiveAsync cancel'
}
