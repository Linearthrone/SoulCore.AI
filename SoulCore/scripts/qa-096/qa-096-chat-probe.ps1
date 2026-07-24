param(
    [string]$ChatText = 'look at me',
    [string]$SessionId = 'qa096-E8-lookat',
    [int]$WaitSec = 18,
    [int]$DrainSec = 20
)
# qa-096-chat-probe.ps1
# Generic QA-096 chat probe: send -> wait WITHOUT receiving -> drain frames
# Authoritative proof: host log at c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log
# Pattern derived from QA-087/089/093. UE is NOT connected -- that's OK; verb client logs the dispatch attempt.
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$UnrealWsUrl = 'ws://127.0.0.1:8888'
$HostLogPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }
function Send-FrameR { param($Ws, [string]$Json); $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json); $seg = [ArraySegment[byte]]::new($bytes); $cts = [System.Threading.CancellationTokenSource]::new(5000); $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(5000) | Out-Null; $cts.Dispose() }
function New-FrameR { param([string]$Type, $Payload); $id = [Guid]::NewGuid().ToString('N'); $ts = [DateTimeOffset]::UtcNow.ToString('O'); return @{ v = 1; type = $Type; id = $id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10 }
function Connect-WsR { param([string]$Url, [int]$TimeoutMs = 8000); $ws = [System.Net.WebSockets.ClientWebSocket]::new(); $cts = [System.Threading.CancellationTokenSource]::new($Timeoutms); $t = $ws.ConnectAsync([Uri]$Url, $cts.Token); if (-not $t.Wait($Timeoutms)) { $cts.Cancel(); $ws.Dispose(); throw "Connect timeout to $Url" }; $cts.Dispose(); if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "Connect failed (state=$($ws.State))" }; return $ws }
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

Write-Output '====== QA-096 chat probe ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)
Write-Output ("ChatText: " + $ChatText + " | SessionId: " + $SessionId)
Write-Output ("Pattern: send -> {0}s no-recv (let inference run) -> drain {1}s" -f $WaitSec, $DrainSec)
Write-Output ("Authoritative proof: host log at " + $HostLogPath)

$h = Get-HealthR
if (-not $h) { Write-Output 'RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " unreal.connected=" + $h.unreal.connected + " inference.provider=" + $h.inference.provider)

$logLineBefore = 0
if (Test-Path $HostLogPath) { $logLineBefore = (Get-Content -LiteralPath $HostLogPath -ErrorAction SilentlyContinue | Measure-Object).Count }
Write-Output ("HostLog lines BEFORE send: " + $logLineBefore)

# UE listener (best-effort; authoritative proof is host log). UE NOT connected expected.
$ueConnected = $false; $ueWs = $null
try { $ueWs = Connect-WsR -Url $UnrealWsUrl -TimeoutMs 3000; $ueConnected = $true; Write-Output 'UE listener connected.' } catch { Write-Output ("UE not reachable (expected): " + $_.Exception.Message) }

# Host WS
$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

# Send chat.send
$frame = New-FrameR -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = $SessionId }
Write-Output ("send: " + $frame)
Send-FrameR -Ws $hostWs -Json $frame
$sendDone = [DateTimeOffset]::UtcNow
Write-Output ("send-done (UTC): " + $sendDone.ToString('O'))

# KEY: wait WITHOUT calling ReceiveAsync (no cancel that aborts the connection)
Write-Output ("Waiting {0}s for inference (no receive, no cancel)..." -f $WaitSec)
Start-Sleep -Seconds $WaitSec

# Now drain all host frames
Write-Output 'Draining host frames...'
$hostFrames = @()
$drainDeadline = [DateTime]::UtcNow.AddSeconds($DrainSec)
while ([DateTime]::UtcNow -lt $drainDeadline) {
    $r = Receive-FrameSafe -Ws $hostWs -WaitMs 3000
    if ($r) { $hostFrames += $r; Write-Output ("host[" + $hostFrames.Count + "]: " + (Short-S $r)) } else { break }
    if ($r -match '"type"\s*:\s*"chat\.done"') { break }
}

# Drain UE frames (best-effort)
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

# AUTHORITATIVE PROOF: host log
Write-Output ''
Write-Output '--- Inspecting host log (lines added since send) ---'
$lookAtLines = @(); $lookAtSeen = $false
$speakLines = @(); $speakSeen = $false
$animLines = @(); $animSeen = $false
$locoLines = @(); $locoSeen = $false
$verbLines = @()
if (Test-Path $HostLogPath) {
    $logAll = Get-Content -LiteralPath $HostLogPath -ErrorAction SilentlyContinue
    $logAfter = $logAll | Select-Object -Skip $logLineBefore -ErrorAction SilentlyContinue
    Write-Output ("HostLog lines AFTER send: " + ($logAll | Measure-Object).Count + " (new: " + ($logAfter | Measure-Object).Count + ")")
    foreach ($l in $logAfter) {
        if ($l -match 'look_at_player') { $lookAtSeen = $true; $lookAtLines += $l }
        if ($l -match 'Unreal look intent dispatched') { $lookAtSeen = $true; $lookAtLines += $l }
        if ($l -match 'soul=speak') { $speakSeen = $true; $speakLines += $l }
        if ($l -match 'Unreal animation intent dispatched') { $animSeen = $true; $animLines += $l }
        if ($l -match 'play_animation') { $animSeen = $true; $animLines += $l }
        if ($l -match 'Unreal loco intent dispatched|move_avatar_relative|soul=loco') { $locoSeen = $true; $locoLines += $l }
        if ($l -match 'Unreal verb sent') { $verbLines += $l }
    }
    Write-Output ("Unreal verb sent lines (all verb dispatches this window): " + $verbLines.Count)
    foreach ($l in $verbLines) { Write-Output ("  " + $l) }
} else {
    Write-Output ("HostLog not found at " + $HostLogPath)
}

# Analyze frames
$chatDoneSeen = $false; $chatDeltaSeen = $false
foreach ($r in $hostFrames) {
    if ($r -match '"type"\s*:\s*"chat\.done"') { $chatDoneSeen = $true }
    if ($r -match '"type"\s*:\s*"chat\.delta"') { $chatDeltaSeen = $true }
}
$inferenceMs = -1
$doneFrame = $hostFrames | Where-Object { $_ -match 'chat\.done' } | Select-Object -First 1
if ($doneFrame -match '"ts":"([^"]+)"') { try { $doneTs = [DateTimeOffset]::Parse($matches[1]).UtcDateTime; $inferenceMs = [int]($doneTs - $sendDone).TotalMilliseconds } catch {} }

$probeEndIso = [DateTimeOffset]::UtcNow.ToString('O')
$evidence = "hostFrames=" + $hostFrames.Count + "; chatDone=" + $chatDoneSeen + "; chatDelta=" + $chatDeltaSeen + "; inferenceMs=" + $inferenceMs + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; lookAtSeenLog=" + $lookAtSeen + "; speakSeenLog=" + $speakSeen + "; animSeenLog=" + $animSeen + "; locoSeenLog=" + $locoSeen
Write-Output ''
Write-Output ("===== PROBE RESULT =====")
Write-Output ("Probe end (UTC): " + $probeEndIso)
Write-Output ("Evidence: " + $evidence)
Write-Output ("Markers: look_at_player=" + $lookAtSeen + " | soul=speak=" + $speakSeen + " | anim=wave=" + $animSeen + " | loco=" + $locoSeen)
Write-Output "================================="
