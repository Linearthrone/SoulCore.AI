# qa-101-E1-E3-E7-E8-regression.ps1
# TASK-101 Step 4: E1/E3/E7/E8 regression. Each test sends a chat.send with the
# prescribed text, waits WITHOUT receiving (no WS cancel), drains, then inspects
# the host log for the expected Information-level dispatch line.
# UE is disconnected (expected); the authoritative proof is the host log dispatch line
# (the verb forwarding happens BEFORE the UE no-op, so the log line fires regardless).
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$HostLogPath = 'c:\Users\kurtw\Soul_Core\SoulCore\scripts\.soulcore-host.log'
$ProbeStartIso = [DateTimeOffset]::UtcNow.ToString('O')

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }

function Send-ChatWaitDrain {
    param([string]$ChatText, [string]$SessionId, [int]$WaitSec = 18)
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $ccts = [System.Threading.CancellationTokenSource]::new(8000)
    $t = $ws.ConnectAsync([Uri]$HostWsUrl, $ccts.Token)
    if (-not $t.Wait(8000)) { $ccts.Cancel(); $ws.Dispose(); throw "connect timeout" }
    $ccts.Dispose()
    if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "connect state=$($ws.State)" }

    $id = [Guid]::NewGuid().ToString('N')
    $ts = [DateTimeOffset]::UtcNow.ToString('O')
    $payload = @{ v = 1; type = 'chat.send'; id = $id; ts = $ts; payload = @{ text = $ChatText; sessionId = $SessionId } } | ConvertTo-Json -Compress -Depth 10
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $seg = [ArraySegment[byte]]::new($bytes)
    $scts = [System.Threading.CancellationTokenSource]::new(5000)
    $st = $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $scts.Token)
    $st.Wait(5000) | Out-Null
    $scts.Dispose()
    $sendDone = [DateTimeOffset]::UtcNow

    Write-Output ("  sent chat.send text='" + $ChatText + "' sid=" + $SessionId + " at " + $sendDone.ToString('O'))
    Start-Sleep -Seconds $WaitSec

    # Drain frames
    $frames = @()
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { break }
        $buf = New-Object byte[] 32768
        $rseg = [ArraySegment[byte]]::new($buf)
        $rcts = [System.Threading.CancellationTokenSource]::new(2500)
        $rt = $ws.ReceiveAsync($rseg, $rcts.Token)
        try {
            if ($rt.Wait(2500)) {
                $rcts.Dispose()
                $cnt = $rt.Result.Count
                if ($cnt -eq 0) { break }
                $frames += [System.Text.Encoding]::UTF8.GetString($buf, 0, $cnt)
                if ($frames[-1] -match '"type"\s*:\s*"chat\.done"') { break }
            } else { $rcts.Cancel(); $rcts.Dispose(); break }
        } catch { try { $rcts.Cancel() } catch {}; try { $rcts.Dispose() } catch {}; break }
    }

    if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try { $cl = [System.Threading.CancellationTokenSource]::new(1500); $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$cl.Token).Wait(1500); $cl.Dispose() } catch {}
    }
    $ws.Dispose()
    return @{ frames = $frames; sendDone = $sendDone }
}

Write-Output '====== E1/E3/E7/E8 QA-101: regression (host-log authoritative) ======'
Write-Output ("Probe start (UTC): " + $ProbeStartIso)

$h = Get-HealthR
if (-not $h) { Write-Output 'REGRESSION RESULT: Fail (Host /health unreachable)'; exit 1 }
Write-Output ("Host health: status=" + $h.status + " unreal.connected=" + $h.unreal.connected + " inference.provider=" + $h.inference.provider)

# We run the 4 tests; each records its own before-log line count.
function Run-One {
    param([string]$Label, [string]$ChatText, [string]$LogPattern, [int]$WaitSec = 18)
    Write-Output ''
    Write-Output ("===== " + $Label + " =====")
    $before = 0
    if (Test-Path $HostLogPath) { $before = (Get-Content -LiteralPath $HostLogPath -ErrorAction SilentlyContinue | Measure-Object).Count }
    Write-Output ("  HostLog lines BEFORE: " + $before)
    $sid = "qa101-" + $Label
    $r = Send-ChatWaitDrain -ChatText $ChatText -SessionId $sid -WaitSec $WaitSec
    $chatDone = $false
    foreach ($f in $r.frames) { if ($f -match '"type"\s*:\s*"chat\.done"') { $chatDone = $true; break } }
    Write-Output ("  frames=" + ($r.frames | Measure-Object).Count + " chatDone=" + $chatDone)
    # Inspect log
    $matched = @()
    if (Test-Path $HostLogPath) {
        $logAll = Get-Content -LiteralPath $HostLogPath
        $logAfter = $logAll | Select-Object -Skip $before -ErrorAction SilentlyContinue
        foreach ($l in $logAfter) { if ($l -match $LogPattern) { $matched += $l } }
        if ($matched.Count -gt 0) {
            Write-Output ("  HOST LOG EVIDENCE:")
            foreach ($l in $matched) { Write-Output ("    " + $l) }
        } else {
            Write-Output ("  HOST LOG EVIDENCE: NONE matching /" + $LogPattern + "/")
            $tail = $logAll | Select-Object -Last 6 -ErrorAction SilentlyContinue
            foreach ($l in $tail) { Write-Output ("    ctx: " + $l) }
        }
    }
    if ($matched.Count -gt 0) {
        Write-Output ("  " + $Label + " RESULT: Pass")
        return $true
    } else {
        Write-Output ("  " + $Label + " RESULT: Fail")
        return $false
    }
}

$results = @{}
# E1 speak: "hello" -> host log "soul=speak" (the UnrealVerbClientStub logs "soul=speak")
# Per QA-089, the authoritative line is "Unreal verb sent: soul=speak"
$results['E1'] = Run-One -Label 'E1-speak' -ChatText 'hello' -LogPattern 'soul=speak' -WaitSec 18
# E3 loco: "move forward 50" -> "Unreal loco intent dispatched: forward=50"
# Note: DetectLocoIntent sees "move forward" -> forward=50 (the "50" in text is ignored; intent maps to 50)
$results['E3'] = Run-One -Label 'E3-loco' -ChatText 'move forward 50' -LogPattern 'Unreal loco intent dispatched: forward=50' -WaitSec 18
# E7 play_animation: "wave hello" -> "Unreal animation intent dispatched: anim=wave"
$results['E7'] = Run-One -Label 'E7-playanim' -ChatText 'wave hello' -LogPattern 'Unreal animation intent dispatched: anim=wave' -WaitSec 18
# E8 look_at: "look at me" -> "Unreal look intent dispatched: look_at_player"
$results['E8'] = Run-One -Label 'E8-lookat' -ChatText 'look at me' -LogPattern 'Unreal look intent dispatched: look_at_player' -WaitSec 18

Write-Output ''
Write-Output '====== REGRESSION SUMMARY ======'
foreach ($k in @('E1','E3','E7','E8')) {
    Write-Output ($k + ": " + $(if ($results[$k]) { 'Pass' } else { 'Fail' }))
}
$allPass = $results['E1'] -and $results['E3'] -and $results['E7'] -and $results['E8']
Write-Output ("OVERALL: " + $(if ($allPass) { 'Pass' } else { 'Fail' }))
Write-Output ("Probe end (UTC): " + [DateTimeOffset]::UtcNow.ToString('O'))
