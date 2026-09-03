# _qa_e2_robust.ps1
# Robust E2 probe using Task-based async receive with hard timeouts.
# Avoids the Receive-WsFrame polling deadlock by using .Wait() with a hard cancellation.
$ErrorActionPreference = 'Stop'

$HostWsUrl = 'ws://127.0.0.1:7700/ws'
$HostUrl   = 'http://127.0.0.1:7700'

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

# Receive a single frame using .Wait(timeout) instead of polling IsCompleted
function Receive-FrameR {
    param($Ws, [int]$TimeoutMs = 3000)
    $buf = New-Object byte[] 32768
    $seg = [ArraySegment[byte]]::new($buf)
    $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $task = $Ws.ReceiveAsync($seg, $cts.Token)
    if (-not $task.Wait($TimeoutMs)) {
        $cts.Cancel()
        $cts.Dispose()
        throw 'Receive timeout'
    }
    $cts.Dispose()
    $count = $task.Result.Count
    $eom = $task.Result.EndOfMessage
    if ($count -eq 0) { return $null }
    $text = [System.Text.Encoding]::UTF8.GetString($buf, 0, $count)
    # Handle multi-fragment (rare for our small frames)
    if (-not $eom -and $count -gt 0) {
        $sb = New-Object System.Text.StringBuilder
        $null = $sb.Append($text)
        $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
        while (-not $task.Result.EndOfMessage -and [DateTime]::UtcNow -lt $deadline) {
            $cts2 = [System.Threading.CancellationTokenSource]::new(2000)
            $task2 = $Ws.ReceiveAsync($seg, $cts2.Token)
            if ($task2.Wait(2000)) {
                $null = $sb.Append([System.Text.Encoding]::UTF8.GetString($buf, 0, $task2.Result.Count))
                if ($task2.Result.EndOfMessage) { break }
            }
            $cts2.Dispose()
        }
        $text = $sb.ToString()
    }
    return $text
}

Write-Output '====== E2 (robust): Chat -> Host -> set_emotion snapshot ======'

$h = Get-HealthR
if (-not $h) { Write-Output 'E2 RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " unreal.connected=" + $h.unreal.connected)

$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$cts = [System.Threading.CancellationTokenSource]::new(8000)
$t = $ws.ConnectAsync([Uri]$HostWsUrl, $cts.Token)
if (-not $t.Wait(8000)) { $cts.Cancel(); $ws.Dispose(); Write-Output 'E2 RESULT: Fail (WS connect timeout)'; exit 1 }
$cts.Dispose()
Write-Output 'Host WS connected.'

$frame = New-FrameR -Type 'chat.send' -Payload @{ text = 'I feel calm and content right now'; sessionId = 'e2e-E2-robust' }
Write-Output ("send: " + $frame)
Send-FrameR -Ws $ws -Json $frame

$hostFrames = @()
$deadline = [DateTime]::UtcNow.AddSeconds(12)
while ([DateTime]::UtcNow -lt $deadline) {
    try {
        $r = Receive-FrameR -Ws $ws -TimeoutMs 2000
        if ($r) { $hostFrames += $r; Write-Output ("host-frame: " + ($r -replace '\s+',' ').Substring(0,[Math]::Min(220,$r.Length))) }
    } catch { }
}

# close
try { $c2=[System.Threading.CancellationTokenSource]::new(2000); $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c2.Token).Wait(2000); $c2.Dispose() } catch { }
$ws.Dispose()

$hostSnapshotSeen = $false
foreach ($f in $hostFrames) { if ($f -match '"emotion.snapshot"') { $hostSnapshotSeen = $true; break } }

$evidence = "hostFrames=" + $hostFrames.Count + "; hostSnapshotSeen=" + $hostSnapshotSeen
Write-Output ''
Write-Output ("===== E2 RESULT =====")
if ($hostSnapshotSeen) {
    Write-Output ("Result:   Pass")
    Write-Output ("Evidence: " + $evidence + " (emotion.snapshot emitted by Host -> set_emotion wire path active)")
} else {
    Write-Output ("Result:   Fail")
    Write-Output ("Evidence: " + $evidence + " HARD-STOP: no emotion.snapshot from Host")
}
Write-Output "================================="
