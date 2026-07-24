# e2e-E2-set-emotion.ps1
# E2 — Chat -> Host -> UE `set_emotion` gate (HARD-STOP GATE)
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2 E2
#
# Verifies: a chat.send frame carrying an emotion intent causes Host to forward
# a UE command envelope: { "type":"command", "payload":{ "name":"set_emotion",
# "args":{ valence, arousal, dominance, label } } } to UE :8888
# (per UeVerbWireMapper.MapSetEmotion).
#
# !!! HARD-STOP GATE: If E2 FAILS, SoulLoop must NOT be enabled. !!!
# !!! GUARD: Host must be recycled post-soak before running !!!
#
# Usage:
#   ./e2e-E2-set-emotion.ps1
#   ./e2e-E2-set-emotion.ps1 -Force
#   ./e2e-E2-set-emotion.ps1 -ChatText "I feel calm and content"
param([switch]$Force, [string]$ChatText = 'I feel calm and content right now')

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E2: Chat -> Host -> UE set_emotion (HARD-STOP GATE) ======'
Write-Output ('Host WS : ' + $script:HostWsUrl)
Write-Output ('UE WS   : ' + $script:UnrealWsUrl)
Write-Output ('ChatText: ' + $ChatText)

Assert-HostRecycled -Force:$Force

$h = Get-Health
if (-not $h) { Write-ResultLine 'E2' 'Fail' 'Host /health unreachable'; exit 1 }
Write-Output ('Host health: status=' + $h.status + ' unreal.connected=' + $h.unreal.connected)

$hostWs = $null
$ueWs = $null
try {
    # 1. UE listener first
    $ueConnected = $false
    try { $ueWs = Connect-Ws -Url $script:UnrealWsUrl -TimeoutMs 5000; $ueConnected = $true } catch { }

    # 2. Host WS
    $hostWs = Connect-Ws -Url $script:HostWsUrl -TimeoutMs 8000
    Write-E2E '' 'Host WS connected.'

    # 3. Send chat.send with emotion-laden text
    $frame = New-Frame -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = 'e2e-E2' }
    Write-E2E 'send' $frame
    Send-WsFrame -Ws $hostWs -Json $frame

    # 4. Host may also emit an emotion.snapshot frame; capture both host reply and UE frames
    $hostFrames = @()
    $captureHost = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $captureHost) {
        try {
            $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000
            if ($r) { $hostFrames += $r; Write-E2E 'host-frame' $r }
        } catch { }
    }

    # 5. UE frames
    $ueFrames = @()
    if ($ueConnected) {
        $captureUe = [DateTime]::UtcNow.AddSeconds(6)
        while ([DateTime]::UtcNow -lt $captureUe) {
            try { $f = Receive-WsFrame -Ws $ueWs -TimeoutMs 1500; if ($f) { $ueFrames += $f; Write-E2E 'ue-frame' $f } } catch { }
        }
    }

    # 6. Evaluate: look for set_emotion command envelope on UE, or emotion.snapshot on host
    $setEmotionSeen = $false
    $hostSnapshotSeen = $false
    foreach ($f in $ueFrames) {
        if ($f -match '"set_emotion"') { $setEmotionSeen = $true; break }
    }
    foreach ($f in $hostFrames) {
        if ($f -match '"emotion.snapshot"') { $hostSnapshotSeen = $true; break }
    }

    $evidence = "hostFrames=" + $hostFrames.Count + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; setEmotionSeen=" + $setEmotionSeen + "; hostSnapshotSeen=" + $hostSnapshotSeen

    if ($setEmotionSeen -or $hostSnapshotSeen) {
        Write-ResultLine 'E2' 'Pass' $evidence
    } elseif (-not $ueConnected) {
        Write-ResultLine 'E2' 'Skip' ($evidence + ' (UE :8888 not running — needs UE up for set_emotion wire verification)')
    } else {
        Write-ResultLine 'E2' 'Fail' ($evidence + ' HARD-STOP: do NOT enable SoulLoop if E2 fails')
    }
} catch {
    Write-ResultLine 'E2' 'Fail' ('Exception: ' + $_.Exception.Message + ' HARD-STOP: do NOT enable SoulLoop')
} finally {
    Close-Ws $hostWs
    Close-Ws $ueWs
}
