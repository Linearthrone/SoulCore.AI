# _qa_e3_robust_086.ps1
# QA-086 E3 (loco) robust probe — post BED-085 (MaxTokens=256, NSFW-flash:Q4_K_M, 180s timeout)
# Sends a loco-intent chat.send to Host, listens on UE :8888 for a `move_avatar_relative
# <f> <r> <u>` plain frame. Uses Task.Wait()-based receive to avoid the Receive-WsFrame
# polling deadlock. Inference may take 30-100s+; 185s window matches the Host HttpClient timeout.
# Host and UE sockets drained in an interleaved loop to prevent UE buffer overflow.
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$UnrealWsUrl = 'ws://127.0.0.1:8888'
$ChatText    = 'take a small step forward'
$SessionId   = 'qa086-E3'
$WaitSeconds = 185
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR {
    try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null }
}
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
function Receive-FrameR {
    param($Ws, [int]$TimeoutMs = 1000)
    $buf = New-Object byte[] 32768
    $seg = [ArraySegment[byte]]::new($buf)
    $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $task = $Ws.ReceiveAsync($seg, $cts.Token)
    if (-not $task.Wait($TimeoutMs)) { $cts.Cancel(); $cts.Dispose(); return $null }
    $cts.Dispose()
    $count = $task.Result.Count
    if ($count -eq 0) { return $null }
    return [System.Text.Encoding]::UTF8.GetString($buf, 0, $count)
}
function Short-S {
    param([string]$S, [int]$Len = 220)
    $s2 = ($S -replace '\s+',' ')
    return $s2.Substring(0, [Math]::Min($Len, $s2.Length))
}
function Close-WsR {
    param($Ws)
    if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try { $c=[System.Threading.CancellationTokenSource]::new(2000); $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c.Token).Wait(2000); $c.Dispose() } catch { }
    }
    if ($Ws) { $Ws.Dispose() }
}

Write-Output '====== E3 (robust) QA-086: Chat -> Host -> UE loco ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)
Write-Output ("ChatText: " + $ChatText + " | SessionId: " + $SessionId)
Write-Output ("WaitWindow: " + $WaitSeconds + "s (interleaved host+UE drain)")

$h = Get-HealthR
if (-not $h) { Write-Output 'E3 RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " unreal.connected=" + $h.unreal.connected + " inference.provider=" + $h.inference.provider)

# 1. UE listener first (so we don't miss the forwarded loco frame)
$ueConnected = $false
$ueWs = $null
try {
    $ueWs = Connect-WsR -Url $UnrealWsUrl -TimeoutMs 5000
    $ueConnected = $true
    Write-Output 'UE listener connected.'
} catch {
    Write-Output ("UE listener not reachable: " + $_.Exception.Message)
}

# 2. Host WS
$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

# 3. Send loco-intent chat
$frame = New-FrameR -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = $SessionId }
Write-Output ("send: " + $frame)
Send-FrameR -Ws $hostWs -Json $frame
$sendDoneIso = [DateTimeOffset]::UtcNow.ToString('O')
Write-Output ("send-done (UTC): " + $sendDoneIso)

# 4. Interleaved capture: drain host + UE in the same loop for the full window.
$hostFrames = @()
$ueFrames = @()
$chatDoneSeen = $false
$chatDeltaSeen = $false
$locoSeen = $false
$locoFrame = ''
$deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
$lastBeat = [DateTime]::UtcNow
while ([DateTime]::UtcNow -lt $deadline) {
    # Drain host (short timeout)
    try {
        $r = Receive-FrameR -Ws $hostWs -TimeoutMs 800
        if ($r) {
            $hostFrames += $r
            Write-Output ("host-frame[" + $hostFrames.Count + "]: " + (Short-S $r))
            if ($r -match '"type"\s*:\s*"chat\.done"') { $chatDoneSeen = $true }
            if ($r -match '"type"\s*:\s*"chat\.delta"') { $chatDeltaSeen = $true }
        }
    } catch { }
    # Drain UE (short timeout) — keeps buffer clear and catches loco frame
    if ($ueConnected) {
        try {
            $f = Receive-FrameR -Ws $ueWs -TimeoutMs 800
            if ($f) {
                $ueFrames += $f
                Write-Output ("ue-frame[" + $ueFrames.Count + "]: " + (Short-S $f))
                if ($f -match '^move_avatar_relative\b') { $locoSeen = $true; $locoFrame = $f }
            }
        } catch { }
    }
    # Heartbeat every ~15s
    if (([DateTime]::UtcNow - $lastBeat).TotalSeconds -ge 15) {
        $elapsed = [int]([DateTime]::UtcNow - [DateTimeOffset]::Parse($sendDoneIso).UtcDateTime).TotalSeconds
        Write-Output ("  [heartbeat] " + $elapsed + "s; hostFrames=" + $hostFrames.Count + " ueFrames=" + $ueFrames.Count + " chatDone=" + $chatDoneSeen + " loco=" + $locoSeen)
        $lastBeat = [DateTime]::UtcNow
    }
    # Early exit: if we already saw the loco frame, no need to keep waiting
    if ($locoSeen -and $chatDoneSeen) { break }
}

Close-WsR $hostWs
Close-WsR $ueWs

$probeEndIso = [DateTimeOffset]::UtcNow.ToString('O')
$evidence = "hostFrames=" + $hostFrames.Count + "; chatDone=" + $chatDoneSeen + "; chatDelta=" + $chatDeltaSeen + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; locoSeen=" + $locoSeen
Write-Output ''
Write-Output ("===== E3 RESULT =====")
Write-Output ("Probe end (UTC): " + $probeEndIso)
if ($locoSeen) {
    Write-Output ("Result:   Pass")
    Write-Output ("Evidence: " + $evidence)
    Write-Output ("LocoFrame: " + $locoFrame)
} elseif (-not $ueConnected) {
    Write-Output ("Result:   Skip")
    Write-Output ("Evidence: " + $evidence + " (UE :8888 not running - needs UE up for loco wire verification)")
} else {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " HARD-STOP: do NOT enable SoulLoop if E3 fails")
}
Write-Output "================================="
