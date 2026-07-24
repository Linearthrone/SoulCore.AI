# _qa_e5_robust.ps1
# Robust E5 probe: /health unreal block + WS ping -> presence.status frame.
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

Write-Output '====== E5 (robust): Presence Unreal status surface ======'

$h = Get-HealthR
if (-not $h) { Write-Output 'E5 RESULT: Fail (Host /health unreachable)'; exit 1 }

$unrealBlock = $h.unreal
Write-Output ("/health.unreal: " + ($unrealBlock | ConvertTo-Json -Compress))
$targetPresent = (-not [string]::IsNullOrWhiteSpace($unrealBlock.target))
$connectedFlag = ($null -ne $unrealBlock.connected)
$enabledFlag = ($null -ne $unrealBlock.enabled)

$presenceStatus = $false
$ws = $null
try {
    $ws = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
    $ping = New-FrameR -Type 'ping' -Payload @{}
    Send-FrameR -Ws $ws -Json $ping
    Write-Output 'sent ping'
    $dl = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $dl) {
        try {
            $r = Receive-FrameR -Ws $ws -TimeoutMs 2000
            if ($r) { Write-Output ("frame: " + ($r -replace '\s+',' ').Substring(0,[Math]::Min(200,$r.Length))); if ($r -match '"presence.status"') { $presenceStatus = $true } }
        } catch { }
    }
} catch {
    Write-Output ("WS probe error: " + $_.Exception.Message)
} finally {
    Close-WsR $ws
}

$evidence = "unreal.target=" + $unrealBlock.target + "; unreal.enabled=" + $unrealBlock.enabled + "; unreal.connected=" + $unrealBlock.connected + "; presenceStatusFrame=" + $presenceStatus
Write-Output ''
Write-Output ("===== E5 RESULT =====")
if ($targetPresent -and $enabledFlag -and $connectedFlag) {
    Write-Output ("Result:   Pass")
    Write-Output ("Evidence: " + $evidence)
} else {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " (unreal block incomplete in /health)")
}
Write-Output "================================="
