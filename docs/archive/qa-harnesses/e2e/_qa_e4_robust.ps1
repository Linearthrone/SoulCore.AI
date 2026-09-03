# _qa_e4_robust.ps1
# Robust E4 probe: ping -> capture emotion.snapshot -> emotion.correct -> capture revised snapshot.
# Uses Task.Wait()-based receive to avoid the Receive-WsFrame polling deadlock.
$ErrorActionPreference = 'Stop'

$HostWsUrl = 'ws://127.0.0.1:7700/ws'
$HostUrl   = 'http://127.0.0.1:7700'

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
function Receive-FrameR {
    param($Ws, [int]$TimeoutMs = 2000)
    $buf = New-Object byte[] 32768
    $seg = [ArraySegment[byte]]::new($buf)
    $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $task = $Ws.ReceiveAsync($seg, $cts.Token)
    if (-not $task.Wait($TimeoutMs)) { $cts.Cancel(); $cts.Dispose(); throw 'Receive timeout' }
    $cts.Dispose()
    $count = $task.Result.Count
    if ($count -eq 0) { return $null }
    return [System.Text.Encoding]::UTF8.GetString($buf, 0, $count)
}
function Close-WsR {
    param($Ws)
    if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try { $c=[System.Threading.CancellationTokenSource]::new(2000); $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c.Token).Wait(2000); $c.Dispose() } catch { }
    }
    if ($Ws) { $Ws.Dispose() }
}

Write-Output '====== E4 (robust): Presence emotion strip + correction ======'

$h = Get-HealthR
if (-not $h) { Write-Output 'E4 RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " soulLoop.enabled=" + $h.soulLoop.enabled)

$hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
Write-Output 'Host WS connected.'

# 1. Send ping to solicit presence.status / emotion.snapshot
$ping = New-FrameR -Type 'ping' -Payload @{}
Send-FrameR -Ws $hostWs -Json $ping
Write-Output 'sent ping'

# 2. Collect frames for 6s looking for emotion.snapshot
$frames = @()
$snapshotSeen = $false
$dl = [DateTime]::UtcNow.AddSeconds(6)
while ([DateTime]::UtcNow -lt $dl) {
    try {
        $r = Receive-FrameR -Ws $hostWs -TimeoutMs 2000
        if ($r) { $frames += $r; Write-Output ("frame: " + ($r -replace '\s+',' ').Substring(0,[Math]::Min(200,$r.Length))); if ($r -match '"emotion.snapshot"') { $snapshotSeen = $true } }
    } catch { }
}

# 3. Send emotion.correct
$correct = New-FrameR -Type 'emotion.correct' -Payload @{ valence = -0.4; arousal = 0.7; dominance = 0.3; focus = 0.5; note = 'E2E correction probe' }
Write-Output ("send emotion.correct: " + $correct)
Send-FrameR -Ws $hostWs -Json $correct

# 4. Look for revised emotion.snapshot echo
$correctedSnapshot = $false
$dl2 = [DateTime]::UtcNow.AddSeconds(6)
while ([DateTime]::UtcNow -lt $dl2) {
    try {
        $r = Receive-FrameR -Ws $hostWs -TimeoutMs 2000
        if ($r) { $frames += $r; Write-Output ("frame: " + ($r -replace '\s+',' ').Substring(0,[Math]::Min(200,$r.Length))); if ($r -match '"emotion.snapshot"') { $correctedSnapshot = $true } }
    } catch { }
}

Close-WsR $hostWs

$evidence = "frames=" + $frames.Count + "; initialSnapshot=" + $snapshotSeen + "; correctedSnapshot=" + $correctedSnapshot
Write-Output ''
Write-Output ("===== E4 RESULT =====")
if ($snapshotSeen -and $correctedSnapshot) {
    Write-Output ("Result:   Pass")
    Write-Output ("Evidence: " + $evidence)
} elseif (-not $snapshotSeen) {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " (no initial emotion.snapshot broadcast)")
} else {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " (emotion.correct did not produce revised snapshot)")
}
Write-Output "================================="
