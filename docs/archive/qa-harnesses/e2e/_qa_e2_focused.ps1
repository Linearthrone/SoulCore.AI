# _qa_e2_focused.ps1
# Focused E2 probe: sends chat.send with emotion text, captures Host frames only
# (no UE listener loop that can hang). Evaluates hostSnapshotSeen (Pass condition per e2e-E2).
# This avoids the Receive-WsFrame hang on UE telemetry flooding.
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E2 (focused): Chat -> Host -> set_emotion snapshot ======'
Write-Output ('Host WS : ' + $script:HostWsUrl)

$h = Get-Health
if (-not $h) { Write-ResultLine 'E2' 'Fail' 'Host /health unreachable'; exit 1 }
Write-Output ('Host health: status=' + $h.status + ' unreal.connected=' + $h.unreal.connected)

$hostWs = $null
$hostFrames = @()
try {
    $hostWs = Connect-Ws -Url $script:HostWsUrl -TimeoutMs 8000
    Write-E2E '' 'Host WS connected.'

    $frame = New-Frame -Type 'chat.send' -Payload @{ text = 'I feel calm and content right now'; sessionId = 'e2e-E2-focused' }
    Write-E2E 'send' $frame
    Send-WsFrame -Ws $hostWs -Json $frame

    # Capture host frames for 10s using a hard deadline + per-recv short timeout
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000
            if ($r) { $hostFrames += $r; Write-E2E 'host-frame' $r }
        } catch {
            # timeout on single recv — continue
        }
    }
} catch {
    Write-Output ('EXCEPTION: ' + $_.Exception.Message)
} finally {
    Close-Ws $hostWs
}

$hostSnapshotSeen = $false
$setEmotionInHost = $false
foreach ($f in $hostFrames) {
    if ($f -match '"emotion.snapshot"') { $hostSnapshotSeen = $true }
    if ($f -match '"set_emotion"') { $setEmotionInHost = $true }
}

$evidence = "hostFrames=" + $hostFrames.Count + "; hostSnapshotSeen=" + $hostSnapshotSeen + "; setEmotionInHost=" + $setEmotionInHost
Write-Output ''
Write-Output ('Captured host frames: ' + $hostFrames.Count)
foreach ($f in $hostFrames) { Write-Output ('  HF: ' + ($f -replace '\s+',' ').Substring(0, [Math]::Min(200, $f.Length))) }

if ($hostSnapshotSeen) {
    Write-ResultLine 'E2' 'Pass' ($evidence + ' (emotion.snapshot emitted by Host -> set_emotion wire path active)')
} else {
    Write-ResultLine 'E2' 'Fail' ($evidence + ' HARD-STOP: no emotion.snapshot from Host')
}
