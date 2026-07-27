param(
    [Parameter(Mandatory = $true)][string]$ChatText,
    [Parameter(Mandatory = $true)][string]$SessionId,
    [string]$OutJson = '',
    [int]$WaitSec = 90,
    [int]$DrainSec = 120
)
# qa-107-chat-until-done.ps1
# Pattern: send -> WaitSec no-recv (keep WS open, let inference run) -> drain until chat.done.
# Avoids canceling ReceiveAsync during inference (which can abort the WS).
$ErrorActionPreference = 'Stop'

$HostWsUrl = 'ws://127.0.0.1:7700/ws'
$HostUrl   = 'http://127.0.0.1:7700'
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }
function Send-FrameR {
    param($Ws, [string]$Json)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
    $seg = [ArraySegment[byte]]::new($bytes)
    $cts = [System.Threading.CancellationTokenSource]::new(5000)
    $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(5000) | Out-Null
    $cts.Dispose()
}
function New-FrameR {
    param([string]$Type, $Payload)
    $id = [Guid]::NewGuid().ToString('N')
    $ts = [DateTimeOffset]::UtcNow.ToString('O')
    return @{ v = 1; type = $Type; id = $id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10
}
function Connect-WsR {
    param([string]$Url, [int]$TimeoutMs = 8000)
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $t = $ws.ConnectAsync([Uri]$Url, $cts.Token)
    if (-not $t.Wait($TimeoutMs)) { $cts.Cancel(); $ws.Dispose(); throw "Connect timeout to $Url" }
    $cts.Dispose()
    if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "Connect failed (state=$($ws.State))" }
    return $ws
}
function Short-S {
    param([string]$S, [int]$Len = 500)
    $s2 = ($S -replace '\s+', ' ')
    return $s2.Substring(0, [Math]::Min($Len, $s2.Length))
}
function Close-WsR {
    param($Ws)
    if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try {
            $c = [System.Threading.CancellationTokenSource]::new(2000)
            $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', $c.Token).Wait(2000)
            $c.Dispose()
        } catch { }
    }
    if ($Ws) { $Ws.Dispose() }
}
function Receive-FrameSafe {
    param($Ws, [int]$WaitMs = 5000)
    $buf = New-Object byte[] 65536
    $seg = [ArraySegment[byte]]::new($buf)
    $cts = [System.Threading.CancellationTokenSource]::new($WaitMs)
    $task = $Ws.ReceiveAsync($seg, $cts.Token)
    if ($task.Wait($WaitMs)) {
        $cts.Dispose()
        $count = $task.Result.Count
        if ($count -eq 0) { return $null }
        return [System.Text.Encoding]::UTF8.GetString($buf, 0, $count)
    } else {
        $cts.Cancel(); $cts.Dispose()
        return $null
    }
}

Write-Output '====== QA-107 chat-until-done ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)
Write-Output ("ChatText: " + $ChatText)
Write-Output ("SessionId: " + $SessionId)
Write-Output ("Pattern: send -> ${WaitSec}s no-recv -> drain ${DrainSec}s until chat.done")

$h = Get-HealthR
if (-not $h) { Write-Output 'RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " inference.enabled=" + $h.inference.enabled + " embeddingsEnabled=" + $h.inference.embeddingsEnabled + " embeddingModel=" + $h.inference.embeddingModel)
Write-Output ("memory.path=" + $h.memory.path)

$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

$frame = New-FrameR -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = $SessionId }
Write-Output ("send: " + $frame)
Send-FrameR -Ws $hostWs -Json $frame
$sendDone = [DateTimeOffset]::UtcNow
Write-Output ("send-done (UTC): " + $sendDone.ToString('O'))

Write-Output ("Waiting {0}s for inference (no receive, no cancel; WS stays open)..." -f $WaitSec)
Start-Sleep -Seconds $WaitSec

Write-Output 'Draining host frames until chat.done...'
$hostFrames = @()
$chatDoneSeen = $false
$replyText = ''
$drainDeadline = [DateTime]::UtcNow.AddSeconds($DrainSec)
while ([DateTime]::UtcNow -lt $drainDeadline) {
    $r = Receive-FrameSafe -Ws $hostWs -WaitMs 5000
    if ($r) {
        $hostFrames += $r
        Write-Output ("host[" + $hostFrames.Count + "]: " + (Short-S $r))
        if ($r -match '"type"\s*:\s*"chat\.done"') {
            $chatDoneSeen = $true
            try {
                $j = $r | ConvertFrom-Json
                if ($j.payload -and $j.payload.text) { $replyText = [string]$j.payload.text }
                elseif ($j.payload -and $j.payload.reply) { $replyText = [string]$j.payload.reply }
            } catch { }
            break
        }
    } else {
        # brief idle; keep draining until deadline
        if ($hostFrames.Count -gt 0 -and -not ($hostFrames | Where-Object { $_ -match 'chat\.delta|chat\.done' })) {
            # got something else then silence — continue
        }
    }
}

# Give post-chat embedding store a moment (runs after chat.done on server)
if ($chatDoneSeen) {
    Write-Output 'Post-done settle 5s (embedding store is after chat.done)...'
    Start-Sleep -Seconds 5
}

Close-WsR $hostWs
$probeEndIso = [DateTimeOffset]::UtcNow.ToString('O')
$elapsedSec = [Math]::Round(([DateTimeOffset]::Parse($probeEndIso) - [DateTimeOffset]::Parse($ProbeStartIso)).TotalSeconds, 1)

Write-Output ''
Write-Output '===== RESULT ====='
Write-Output ("Probe end (UTC): " + $probeEndIso)
Write-Output ("ElapsedSec: " + $elapsedSec)
Write-Output ("hostFrames=" + $hostFrames.Count + "; chatDone=" + $chatDoneSeen + "; replyLen=" + $replyText.Length)
if ($replyText) { Write-Output ("ReplyText: " + (Short-S $replyText 800)) }
if ($chatDoneSeen -and $replyText.Length -gt 0) {
    Write-Output 'Result: Pass (chat.done + non-empty reply)'
} elseif ($chatDoneSeen) {
    Write-Output 'Result: Partial (chat.done but empty reply)'
} else {
    Write-Output 'Result: Fail (no chat.done within wait+drain window)'
}

$result = [ordered]@{
    probeStart = $ProbeStartIso
    probeEnd = $probeEndIso
    elapsedSec = $elapsedSec
    sessionId = $SessionId
    chatText = $ChatText
    chatDone = $chatDoneSeen
    replyText = $replyText
    hostFrameCount = $hostFrames.Count
    memoryPath = $h.memory.path
    embeddingsEnabled = [bool]$h.inference.embeddingsEnabled
    embeddingModel = [string]$h.inference.embeddingModel
}
if ($OutJson) {
    ($result | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $OutJson -Encoding UTF8
    Write-Output ("Wrote: " + $OutJson)
}
Write-Output '================================='
if (-not $chatDoneSeen) { exit 2 }
