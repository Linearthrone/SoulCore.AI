# qa-101-E9b-reflection.ps1
# TASK-101 Step 3: Verify SoulLoop episodic reflection log is at Information level.
# ReflectionIntervalTicks=5. We send a series of loop.tick frames (one WS connection
# per tick to avoid stale-socket issues), then inspect the host log for:
#   "SoulLoop episodic reflection written: [Reflection]..."
# and confirm the line is at Information level (prefix "info:").
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$HostLogPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }

function Send-Tick {
    param([int]$TimeoutMs = 8000)
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ccts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $t = $ws.ConnectAsync([Uri]$HostWsUrl, $ccts.Token)
    if (-not $t.Wait($TimeoutMs)) { $ccts.Cancel(); $ws.Dispose(); throw "connect timeout" }
    $ccts.Dispose()
    if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "connect state=$($ws.State)" }

    $id = [Guid]::NewGuid().ToString('N')
    $ts = [DateTimeOffset]::UtcNow.ToString('O')
    $payload = @{ v = 1; type = 'loop.tick'; id = $id; ts = $ts; payload = @{} } | ConvertTo-Json -Compress -Depth 5
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $seg = [ArraySegment[byte]]::new($bytes)
    $scts = [System.Threading.CancellationTokenSource]::new(5000)
    $st = $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $scts.Token)
    $st.Wait(5000) | Out-Null
    $scts.Dispose()

    # Drain a couple frames (loop.want, loop.tick.ok) with a short wait
    $buf = New-Object byte[] 32768
    $rseg = [ArraySegment[byte]]::new($buf)
    $deadline = [DateTime]::UtcNow.AddSeconds(3)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { break }
        $rcts = [System.Threading.CancellationTokenSource]::new(1500)
        $rt = $ws.ReceiveAsync($rseg, $rcts.Token)
        try { if ($rt.Wait(1500)) { $rcts.Dispose() } else { $rcts.Cancel(); $rcts.Dispose(); break } }
        catch { try { $rcts.Cancel() } catch {}; try { $rcts.Dispose() } catch {}; break }
    }

    if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try { $cl = [System.Threading.CancellationTokenSource]::new(1500); $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$cl.Token).Wait(1500); $cl.Dispose() } catch {}
    }
    $ws.Dispose()
}

Write-Output '====== E9b QA-101: SoulLoop reflection log level (Information) ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)
Write-Output ("Strategy: send 8 loop.tick frames (1 connection per tick) to reach tick % 5 == 0")

$h = Get-HealthR
if (-not $h) { Write-Output 'E9b RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " soulLoop.enabled=" + $h.soulLoop.enabled + " tickInterval=" + $h.soulLoop.tickIntervalSeconds)

$logLineBefore = 0
if (Test-Path $HostLogPath) { $logLineBefore = (Get-Content -LiteralPath $HostLogPath -ErrorAction SilentlyContinue | Measure-Object).Count }
$wantBefore = 0
if (Test-Path $HostLogPath) { $wantBefore = ((Get-Content -LiteralPath $HostLogPath) | Where-Object { $_ -match 'SoulLoop want\[' } | Measure-Object).Count }
Write-Output ("HostLog lines BEFORE ticks: " + $logLineBefore + " | want[] BEFORE: " + $wantBefore)

$ticksSent = 0
for ($i = 1; $i -le 8; $i++) {
    try {
        Send-Tick -TimeoutMs 8000
        $ticksSent++
        Write-Output ("sent loop.tick #" + $i + " OK")
    } catch {
        Write-Output ("sent loop.tick #" + $i + " FAILED: " + $_.Exception.Message)
    }
    Start-Sleep -Milliseconds 1500
}
Write-Output ("Total loop.tick frames sent OK: " + $ticksSent)
Start-Sleep -Seconds 2

Write-Output ''
Write-Output '--- Inspecting host log for reflection evidence ---'
$reflectionLines = @(); $wantLines = @(); $reflectionInfoSeen = $false; $reflectionDebugSeen = $false
if (Test-Path $HostLogPath) {
    $logAll = Get-Content -LiteralPath $HostLogPath
    $logAfter = $logAll | Select-Object -Skip $logLineBefore -ErrorAction SilentlyContinue
    Write-Output ("HostLog lines AFTER ticks: " + ($logAll | Measure-Object).Count + " (new: " + ($logAfter | Measure-Object).Count + ")")
    foreach ($l in $logAfter) {
        if ($l -match 'SoulLoop episodic reflection written') {
            $reflectionLines += $l
            if ($l -match '^\s*info:') { $reflectionInfoSeen = $true }
            if ($l -match '^\s*dbug:' -or $l -match '^\s*debug:') { $reflectionDebugSeen = $true }
        }
        if ($l -match 'SoulLoop want\[') { $wantLines += $l }
    }
    Write-Output ("SoulLoop want[] lines AFTER: " + ($wantLines | Measure-Object).Count + " (total ticks this probe: " + (($wantLines | Measure-Object).Count) + ")")
    if ($reflectionLines.Count -gt 0) {
        Write-Output ("HOST LOG EVIDENCE (reflection):")
        foreach ($l in $reflectionLines) { Write-Output ("  " + $l) }
    } else {
        Write-Output 'HOST LOG EVIDENCE (reflection): NONE found in new log lines'
    }
    Write-Output '--- Last 25 host log lines (context) ---'
    $tail = $logAll | Select-Object -Last 25 -ErrorAction SilentlyContinue
    foreach ($l in $tail) { Write-Output ("  " + $l) }
} else {
    Write-Output ("HostLog not found at " + $HostLogPath)
}

$probeEndIso = [DateTimeOffset]::UtcNow.ToString('O')
Write-Output ''
Write-Output ("===== E9b RESULT =====")
Write-Output ("Probe end (UTC): " + $probeEndIso)
$evidence = "ticksSent=" + $ticksSent + "; wantLinesAfter=" + ($wantLines | Measure-Object).Count + "; reflectionLines=" + ($reflectionLines | Measure-Object).Count + "; reflectionInfoSeen=" + $reflectionInfoSeen + "; reflectionDebugSeen=" + $reflectionDebugSeen
Write-Output ("Evidence: " + $evidence)
if ($reflectionInfoSeen) {
    Write-Output ("Result:   Pass (reflection log line present at Information level)")
} elseif ($reflectionLines.Count -gt 0) {
    Write-Output ("Result:   Fail (reflection log line present but NOT at Information level - debug=" + $reflectionDebugSeen + ")")
} else {
    Write-Output ("Result:   Fail (no reflection log line found after " + $ticksSent + " ticks)")
}
Write-Output "================================="
