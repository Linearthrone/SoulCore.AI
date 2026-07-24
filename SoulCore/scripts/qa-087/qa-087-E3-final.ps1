# qa-087-E3-final.ps1
# QA-087 E3 (loco) FINAL — uses non-canceling receive pattern.
# Same fix as E1: no ReceiveAsync cancel during inference.
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$UnrealWsUrl = 'ws://127.0.0.1:8888'
$ChatText    = 'take a small step forward'
$SessionId   = 'qa087-E3-final'
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }
function Send-FrameR { param($Ws, [string]$Json); $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json); $seg = [ArraySegment[byte]]::new($bytes); $cts = [System.Threading.CancellationTokenSource]::new(5000); $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(5000) | Out-Null; $cts.Dispose() }
function New-FrameR { param([string]$Type, $Payload); $id = [Guid]::NewGuid().ToString('N'); $ts = [DateTimeOffset]::UtcNow.ToString('O'); return @{ v = 1; type = $Type; id = $id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10 }
function Connect-WsR { param([string]$Url, [int]$TimeoutMs = 8000); $ws = [System.Net.WebSockets.ClientWebSocket]::new(); $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs); $t = $ws.ConnectAsync([Uri]$Url, $cts.Token); if (-not $t.Wait($TimeoutMs)) { $cts.Cancel(); $ws.Dispose(); throw "Connect timeout to $Url" }; $cts.Dispose(); if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "Connect failed (state=$($ws.State))" }; return $ws }
function Short-S { param([string]$S, [int]$Len = 220); $s2 = ($S -replace '\s+',' '); return $s2.Substring(0, [Math]::Min($Len, $s2.Length)) }
function Close-WsR { param($Ws); if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) { try { $c=[System.Threading.CancellationTokenSource]::new(2000); $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c.Token).Wait(2000); $c.Dispose() } catch { } }; if ($Ws) { $Ws.Dispose() } }
function Receive-FrameSafe { param($Ws, [int]$WaitMs = 5000)
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
        $cts.Cancel(); $cts.Dispose()
        return $null
    }
}

Write-Output '====== E3 FINAL QA-087: Chat -> Host -> UE loco ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)
Write-Output ("ChatText: " + $ChatText + " | SessionId: " + $SessionId)
Write-Output ("Pattern: send -> 15s no-recv (let inference run) -> drain frames")

$h = Get-HealthR
if (-not $h) { Write-Output 'E3 RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " unreal.connected=" + $h.unreal.connected + " inference.provider=" + $h.inference.provider)

$ueConnected = $false; $ueWs = $null
try { $ueWs = Connect-WsR -Url $UnrealWsUrl -TimeoutMs 5000; $ueConnected = $true; Write-Output 'UE listener connected.' } catch { Write-Output ("UE not reachable: " + $_.Exception.Message) }

$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

$frame = New-FrameR -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = $SessionId }
Write-Output ("send: " + $frame)
Send-FrameR -Ws $hostWs -Json $frame
$sendDone = [DateTimeOffset]::UtcNow
Write-Output ("send-done (UTC): " + $sendDone.ToString('O'))

Write-Output 'Waiting 15s for inference (no receive, no cancel)...'
Start-Sleep -Seconds 15

Write-Output 'Draining host frames...'
$hostFrames = @()
$drainDeadline = [DateTime]::UtcNow.AddSeconds(20)
while ([DateTime]::UtcNow -lt $drainDeadline) {
    $r = Receive-FrameSafe -Ws $hostWs -WaitMs 3000
    if ($r) { $hostFrames += $r; Write-Output ("host[" + $hostFrames.Count + "]: " + (Short-S $r)) } else { break }
    if ($r -match '"type"\s*:\s*"chat\.done"') { break }
}

$ueFrames = @()
if ($ueConnected) {
    Write-Output 'Draining UE frames...'
    $ueDeadline = [DateTime]::UtcNow.AddSeconds(8)
    while ([DateTime]::UtcNow -lt $ueDeadline) {
        $f = Receive-FrameSafe -Ws $ueWs -WaitMs 2000
        if ($f) { $ueFrames += $f; Write-Output ("ue[" + $ueFrames.Count + "]: " + (Short-S $f)) } else { break }
    }
}

Close-WsR $hostWs; Close-WsR $ueWs

$chatDoneSeen = $false; $chatDeltaSeen = $false
$locoSeen = $false; $locoFrame = ''; $speakSeen = $false; $speakFrame = ''; $setEmotionSeen = $false; $ackSeen = $false; $ackFrame = ''
foreach ($r in $hostFrames) {
    if ($r -match '"type"\s*:\s*"chat\.done"') { $chatDoneSeen = $true }
    if ($r -match '"type"\s*:\s*"chat\.delta"') { $chatDeltaSeen = $true }
}
foreach ($f in $ueFrames) {
    if ($f -match '^move_avatar_relative\b') { $locoSeen = $true; $locoFrame = $f }
    if ($f -match '^speak\b') { $speakSeen = $true; $speakFrame = $f }
    if ($f -match '"name"\s*:\s*"set_emotion"') { $setEmotionSeen = $true }
    if ($f -match '"type"\s*:\s*"ack"') { $ackSeen = $true; $ackFrame = $f }
}
$inferenceMs = -1
$doneFrame = $hostFrames | Where-Object { $_ -match 'chat\.done' } | Select-Object -First 1
if ($doneFrame -match '"ts":"([^"]+)"') { try { $doneTs = [DateTimeOffset]::Parse($matches[1]).UtcDateTime; $inferenceMs = [int]($doneTs - $sendDone).TotalMilliseconds } catch {} }

$probeEndIso = [DateTimeOffset]::UtcNow.ToString('O')
$evidence = "hostFrames=" + $hostFrames.Count + "; chatDone=" + $chatDoneSeen + "; chatDelta=" + $chatDeltaSeen + "; inferenceMs=" + $inferenceMs + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; locoSeen=" + $locoSeen + "; speakSeen=" + $speakSeen + "; setEmotionSeen=" + $setEmotionSeen + "; ackSeen=" + $ackSeen
Write-Output ''
Write-Output ("===== E3 RESULT =====")
Write-Output ("Probe end (UTC): " + $probeEndIso)
Write-Output ("Evidence: " + $evidence)
if ($locoFrame) { Write-Output ("LocoFrame: " + $locoFrame) }
if ($speakFrame) { Write-Output ("SpeakFrame: " + $speakFrame) }
if ($ackFrame) { Write-Output ("AckFrame: " + $ackFrame) }
if ($locoSeen) {
    Write-Output ("Result:   Pass")
} elseif ($chatDoneSeen -and -not $ueConnected) {
    Write-Output ("Result:   Fail (inference ok but UE not connected)")
} elseif ($chatDoneSeen) {
    Write-Output ("Result:   Fail (inference ok but no move_avatar_relative on UE - chat path does not call LocoAsync)")
    Write-Output ("Note: HandleChatSendAsync only calls SetEmotionAsync + SpeakAsync, not LocoAsync. No intent router exists.")
} else {
    Write-Output ("Result:   Fail (inference timeout)")
}
Write-Output "================================="
