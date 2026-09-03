# _qa_e6_robust.ps1
# Robust E6 probe: SoulLoop OFF -> loop.tick ack only, NO loop.want.
$ErrorActionPreference = 'Stop'

$HostWsUrl = 'ws://127.0.0.1:7700/ws'
$HostUrl   = 'http://127.0.0.1:7700'

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }
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
function Send-FrameR {
    param($Ws, [string]$Json)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
    $seg = [ArraySegment[byte]]::new($bytes)
    $cts = [System.Threading.CancellationTokenSource]::new(5000)
    $task = $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token)
    $ok = $task.Wait(5000); $cts.Dispose()
    if (-not $ok) { throw 'Send timeout' }
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

Write-Output '====== E6 (robust): Want strip placeholder (SoulLoop OFF) ======'

$h = Get-HealthR
if (-not $h) { Write-Output 'E6 RESULT: Fail (Host /health unreachable)'; exit 1 }
$soulLoopOff = ($h.soulLoop.enabled -eq $false)
Write-Output ("/health.soulLoop.enabled = " + $h.soulLoop.enabled)

$ws = $null
$wantEmitted = $false
$tickOk = $false
$frames = @()
try {
    $ws = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
    Write-Output 'Host WS connected.'

    $tick = New-FrameR -Type 'loop.tick' -Payload @{}
    Write-Output ("send: " + $tick)
    Send-FrameR -Ws $ws -Json $tick

    $dl = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $dl) {
        try {
            $r = Receive-FrameR -Ws $ws -TimeoutMs 2000
            if ($r) {
                $frames += $r
                Write-Output ("frame: " + ($r -replace '\s+',' ').Substring(0,[Math]::Min(200,$r.Length)))
                if ($r -match '"loop.tick.ok"') { $tickOk = $true }
                if ($r -match '"loop.want"') { $wantEmitted = $true }
            }
        } catch { }
    }
} catch {
    Write-Output ("Exception: " + $_.Exception.Message)
} finally {
    Close-WsR $ws
}

$evidence = "soulLoopOff=" + $soulLoopOff + "; tickOk=" + $tickOk + "; wantEmitted=" + $wantEmitted + "; frames=" + $frames.Count
Write-Output ''
Write-Output ("===== E6 RESULT =====")
if ($soulLoopOff -and (-not $wantEmitted)) {
    Write-Output ("Result:   Pass")
    Write-Output ("Evidence: " + $evidence + " (Want strip stays placeholder while SoulLoop off)")
} elseif ($wantEmitted) {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " HARD-STOP: loop.want emitted while SoulLoop disabled - want strip would show live want")
} else {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " (SoulLoop unexpectedly enabled)")
}
Write-Output "================================="
