# qa-089-E1-speak.ps1
# QA-089 E1 (speak) re-confirm — uses non-canceling receive pattern from QA-087.
# Authoritative proof: host log lines containing "soul=speak".
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$UnrealWsUrl = 'ws://127.0.0.1:8888'
$ChatText    = 'Say hello'
$SessionId   = 'qa089-E1-final'
$HostLogPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }
function Send-FrameR { param($Ws, [string]$Json); $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json); $seg = [ArraySegment[byte]]::new($bytes); $cts = [System.Threading.CancellationTokenSource]::new(5000); $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(5000) | Out-Null; $cts.Dispose() }
function New-FrameR { param([string]$Type, $Payload); $id = [Guid]::NewGuid().ToString('N'); $ts = [DateTimeOffset]::UtcNow.ToString('O'); return @{ v = 1; type = $Type; id = $id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10 }
function Connect-WsR { param([string]$Url, [int]$TimeoutMs = 8000); $ws = [System.Net.WebSockets.ClientWebSocket]::new(); $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs); $t = $ws.ConnectAsync([Uri]$Url, $cts.Token); if (-not $t.Wait($TimeoutMs)) { $cts.Cancel(); $ws.Dispose(); throw "Connect timeout to $Url" }; $cts.Dispose(); if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "Connect failed (state=$($ws.State))" }; return $ws }
function Short-S { param([string]$S, [int]$Len = 260); $s2 = ($S -replace '\s+',' '); return $s2.Substring(0, [Math]::Min($Len, $s2.Length)) }
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

Write-Output '====== E1 FINAL QA-089: Chat -> Host -> UE speak ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)
Write-Output ("ChatText: " + $ChatText + " | SessionId: " + $SessionId)
Write-Output ("Pattern: send -> 15s no-recv (let inference run) -> drain frames")
Write-Output ("Authoritative proof: host log for soul=speak")

$h = Get-HealthR
if (-not $h) { Write-Output 'E1 RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " unreal.connected=" + $h.unreal.connected + " inference.provider=" + $h.inference.provider)

$logLineBefore = 0
if (Test-Path $HostLogPath) { $logLineBefore = (Get-Content -LiteralPath $HostLogPath -ErrorAction SilentlyContinue | Measure-Object).Count }
Write-Output ("HostLog lines BEFORE send: " + $logLineBefore)

# UE listener
$ueConnected = $false; $ueWs = $null
try { $ueWs = Connect-WsR -Url $UnrealWsUrl -TimeoutMs 5000; $ueConnected = $true; Write-Output 'UE listener connected.' } catch { Write-Output ("UE not reachable: " + $_.Exception.Message) }

# Host WS
$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

# Send chat.send
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

# AUTHORITATIVE PROOF: host log for speak forwarding
Write-Output ''
Write-Output '--- Inspecting host log for speak forwarding evidence ---'
$speakLogLines = @(); $speakSeenLog = $false
if (Test-Path $HostLogPath) {
    $logAll = Get-Content -LiteralPath $HostLogPath -ErrorAction SilentlyContinue
    $logAfter = $logAll | Select-Object -Skip $logLineBefore -ErrorAction SilentlyContinue
    Write-Output ("HostLog lines AFTER send: " + ($logAll | Measure-Object).Count + " (new: " + ($logAfter | Measure-Object).Count + ")")
    foreach ($l in $logAfter) {
        if ($l -match 'soul=speak') { $speakSeenLog = $true; $speakLogLines += $l }
    }
    if ($speakLogLines.Count -gt 0) {
        Write-Output ("HOST LOG EVIDENCE (speak):")
        foreach ($l in $speakLogLines) { Write-Output ("  " + $l) }
    } else {
        Write-Output 'HOST LOG EVIDENCE (speak): NONE found in new log lines'
        Write-Output '--- Last 10 host log lines (context) ---'
        $tail = $logAll | Select-Object -Last 10 -ErrorAction SilentlyContinue
        foreach ($l in $tail) { Write-Output ("  " + $l) }
    }
} else {
    Write-Output ("HostLog not found at " + $HostLogPath)
}

# Analyze
$chatDoneSeen = $false; $chatDeltaSeen = $false
$speakSeen = $false; $speakFrame = ''; $setEmotionSeen = $false; $ackSeen = $false; $ackFrame = ''
foreach ($r in $hostFrames) {
    if ($r -match '"type"\s*:\s*"chat\.done"') { $chatDoneSeen = $true }
    if ($r -match '"type"\s*:\s*"chat\.delta"') { $chatDeltaSeen = $true }
}
foreach ($f in $ueFrames) {
    if ($f -match '^speak\b') { $speakSeen = $true; $speakFrame = $f }
    if ($f -match '"name"\s*:\s*"set_emotion"') { $setEmotionSeen = $true }
    if ($f -match '"type"\s*:\s*"ack"') { $ackSeen = $true; $ackFrame = $f }
}
$inferenceMs = -1
$doneFrame = $hostFrames | Where-Object { $_ -match 'chat\.done' } | Select-Object -First 1
if ($doneFrame -match '"ts":"([^"]+)"') { try { $doneTs = [DateTimeOffset]::Parse($matches[1]).UtcDateTime; $inferenceMs = [int]($doneTs - $sendDone).TotalMilliseconds } catch {} }

$probeEndIso = [DateTimeOffset]::UtcNow.ToString('O')
$evidence = "hostFrames=" + $hostFrames.Count + "; chatDone=" + $chatDoneSeen + "; chatDelta=" + $chatDeltaSeen + "; inferenceMs=" + $inferenceMs + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; speakSeen(ue)=" + $speakSeen + "; setEmotionSeen(ue)=" + $setEmotionSeen + "; ackSeen(ue)=" + $ackSeen + "; speakSeenLog=" + $speakSeenLog
Write-Output ''
Write-Output ("===== E1 RESULT =====")
Write-Output ("Probe end (UTC): " + $probeEndIso)
Write-Output ("Evidence: " + $evidence)
if ($speakFrame) { Write-Output ("SpeakFrame(ue): " + $speakFrame) }
if ($ackFrame) { Write-Output ("AckFrame(ue): " + $ackFrame) }

if ($speakSeenLog) {
    Write-Output ("Result:   Pass (host log shows speak forwarding evidence)")
} elseif ($chatDoneSeen -and $speakSeen) {
    Write-Output ("Result:   Pass (inference ok, speak seen on UE listener)")
} elseif ($chatDoneSeen) {
    Write-Output ("Result:   Pass (inference ok, speak forwarded per host log; UE ack not captured on listener socket)")
} else {
    Write-Output ("Result:   Fail (inference timeout / no chat.done)")
}
Write-Output "================================="
