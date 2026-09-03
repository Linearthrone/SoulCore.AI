# Combined: warm Ollama, then immediately E1, then immediately E3 (back-to-back, model stays warm)
$ErrorActionPreference = 'Stop'

$HostWsUrl   = 'ws://127.0.0.1:7700/ws'
$HostUrl     = 'http://127.0.0.1:7700'
$UnrealWsUrl = 'ws://127.0.0.1:8888'
$WaitSeconds = 60  # shorter window; model is warm, should respond in <2s

function Get-HealthR { try { return Invoke-RestMethod -Uri "$HostUrl/health" -TimeoutSec 5 } catch { return $null } }
function Send-FrameR { param($Ws, [string]$Json); $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json); $seg = [ArraySegment[byte]]::new($bytes); $cts = [System.Threading.CancellationTokenSource]::new(5000); $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(5000) | Out-Null; $cts.Dispose() }
function New-FrameR { param([string]$Type, $Payload); $id = [Guid]::NewGuid().ToString('N'); $ts = [DateTimeOffset]::UtcNow.ToString('O'); return @{ v = 1; type = $Type; id = $id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10 }
function Connect-WsR { param([string]$Url, [int]$TimeoutMs = 8000); $ws = [System.Net.WebSockets.ClientWebSocket]::new(); $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs); $t = $ws.ConnectAsync([Uri]$Url, $cts.Token); if (-not $t.Wait($TimeoutMs)) { $cts.Cancel(); $ws.Dispose(); throw "Connect timeout to $Url" }; $cts.Dispose(); if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) { $ws.Dispose(); throw "Connect failed" }; return $ws }
function Receive-FrameR { param($Ws, [int]$TimeoutMs = 1000); $buf = New-Object byte[] 32768; $seg = [ArraySegment[byte]]::new($buf); $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs); $task = $Ws.ReceiveAsync($seg, $cts.Token); if (-not $task.Wait($TimeoutMs)) { $cts.Cancel(); $cts.Dispose(); return $null }; $cts.Dispose(); $count = $task.Result.Count; if ($count -eq 0) { return $null }; return [System.Text.Encoding]::UTF8.GetString($buf, 0, $count) }
function Short-S { param([string]$S, [int]$Len = 200); $s2 = ($S -replace '\s+',' '); return $s2.Substring(0, [Math]::Min($Len, $s2.Length)) }
function Close-WsR { param($Ws); if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) { try { $c=[System.Threading.CancellationTokenSource]::new(2000); $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'bye',$c.Token).Wait(2000); $c.Dispose() } catch { } }; if ($Ws) { $Ws.Dispose() } }

function Run-ChatProbe {
    param([string]$Label, [string]$ChatText, [string]$SessionId, [int]$WaitSec)
    Write-Output ''
    Write-Output ('====== ' + $Label + ': text="' + $ChatText + '" ======')
    $ueConnected = $false; $ueWs = $null
    try { $ueWs = Connect-WsR -Url $UnrealWsUrl -TimeoutMs 5000; $ueConnected = $true; Write-Output 'UE listener connected.' } catch { Write-Output ('UE not reachable: ' + $_.Exception.Message) }
    $hostWs = Connect-WsR -Url $HostWsUrl -TimeoutMs 8000
    Write-Output 'Host WS connected.'
    $frame = New-FrameR -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = $SessionId }
    Write-Output ('send: ' + $frame)
    Send-FrameR -Ws $hostWs -Json $frame
    $sendDone = [DateTimeOffset]::UtcNow

    $hostFrames = @(); $ueFrames = @()
    $chatDoneSeen = $false; $chatDeltaSeen = $false
    $speakSeen = $false; $speakFrame = ''; $locoSeen = $false; $locoFrame = ''; $setEmotionSeen = $false; $ackSeen = $false
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitSec)
    while ([DateTime]::UtcNow -lt $deadline) {
        try { $r = Receive-FrameR -Ws $hostWs -TimeoutMs 800; if ($r) { $hostFrames += $r; Write-Output ('host[' + $hostFrames.Count + ']: ' + (Short-S $r)); if ($r -match '"type"\s*:\s*"chat\.done"') { $chatDoneSeen = $true }; if ($r -match '"type"\s*:\s*"chat\.delta"') { $chatDeltaSeen = $true } } } catch { }
        if ($ueConnected) { try { $f = Receive-FrameR -Ws $ueWs -TimeoutMs 800; if ($f) { $ueFrames += $f; Write-Output ('ue[' + $ueFrames.Count + ']: ' + (Short-S $f)); if ($f -match '^move_avatar_relative\b') { $locoSeen = $true; $locoFrame = $f }; if ($f -match '^speak\b') { $speakSeen = $true; $speakFrame = $f }; if ($f -match '"name"\s*:\s*"set_emotion"') { $setEmotionSeen = $true }; if ($f -match '"type"\s*:\s*"ack"') { $ackSeen = $true } } } catch { } }
        if ($chatDoneSeen) { $sinceDone = ([DateTime]::UtcNow - $sendDone).TotalSeconds; if ($sinceDone -gt 8) { break } }
    }
    Close-WsR $hostWs; Close-WsR $ueWs
    $inferenceMs = -1
    if ($chatDoneSeen) {
        $doneFrame = $hostFrames | Where-Object { $_ -match 'chat\.done' } | Select-Object -First 1
        if ($doneFrame -match '"ts":"([^"]+)"') {
            $tsStr = $matches[1]
            try { $doneTs = [DateTimeOffset]::Parse($tsStr).UtcDateTime; $inferenceMs = [int]($doneTs - $sendDone).TotalMilliseconds } catch { $inferenceMs = -2 }
        }
    }
    $ev = "hostFrames=" + $hostFrames.Count + "; chatDone=" + $chatDoneSeen + "; chatDelta=" + $chatDeltaSeen + "; inferenceMs=" + $inferenceMs + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; speakSeen=" + $speakSeen + "; locoSeen=" + $locoSeen + "; setEmotionSeen=" + $setEmotionSeen + "; ackSeen=" + $ackSeen
    Write-Output ('EVIDENCE: ' + $ev)
    if ($speakFrame) { Write-Output ('SPEAK_FRAME: ' + $speakFrame) }
    if ($locoFrame) { Write-Output ('LOCO_FRAME: ' + $locoFrame) }
    return @{ chatDone = $chatDoneSeen; speakSeen = $speakSeen; locoSeen = $locoSeen; setEmotionSeen = $setEmotionSeen; ackSeen = $ackSeen; hostFrames = $hostFrames.Count; ueFrames = $ueFrames.Count; inferenceMs = $inferenceMs }
}

# 0. Warm Ollama
Write-Output '=== Warming Ollama ==='
$body = @{ model='hf.co/UnfilteredAI/NSFW-flash:Q4_K_M'; prompt='Say hello'; stream=$false; options=@{ num_predict=20 } } | ConvertTo-Json -Depth 5
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try { Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/generate' -Method Post -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 90 | Out-Null; $sw.Stop(); Write-Output ('Warm OK in ' + $sw.ElapsedMilliseconds + 'ms') } catch { $sw.Stop(); Write-Output ('Warm ERR: ' + $_.Exception.Message) }

# 1. E1
$e1 = Run-ChatProbe -Label 'E1-speak' -ChatText 'Say hello' -SessionId 'qa087-E1b' -WaitSec 60

# 2. E3 immediately (model still warm)
$e3 = Run-ChatProbe -Label 'E3-loco' -ChatText 'take a small step forward' -SessionId 'qa087-E3b' -WaitSec 60

Write-Output ''
Write-Output '===== SUMMARY ====='
Write-Output ('E1: chatDone=' + $e1.chatDone + ' speakSeen=' + $e1.speakSeen + ' setEmotionSeen=' + $e1.setEmotionSeen + ' ackSeen=' + $e1.ackSeen + ' inferenceMs=' + $e1.inferenceMs)
Write-Output ('E3: chatDone=' + $e3.chatDone + ' locoSeen=' + $e3.locoSeen + ' speakSeen=' + $e3.speakSeen + ' setEmotionSeen=' + $e3.setEmotionSeen + ' ackSeen=' + $e3.ackSeen + ' inferenceMs=' + $e3.inferenceMs)
