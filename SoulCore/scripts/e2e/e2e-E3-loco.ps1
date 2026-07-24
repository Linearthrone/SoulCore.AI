# e2e-E3-loco.ps1
# E3 — Chat -> Host -> UE loco gate (HARD-STOP GATE)
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2 E3
#
# Verifies: a chat.send frame carrying a loco/move intent causes Host to forward
# a plain `move_avatar_relative <f> <r> <u>` frame to UE :8888
# (per UeVerbWireMapper.MapLoco).
#
# !!! HARD-STOP GATE: If E3 FAILS, SoulLoop must NOT be enabled. !!!
# !!! GUARD: Host must be recycled post-soak before running !!!
#
# Usage:
#   ./e2e-E3-loco.ps1
#   ./e2e-E3-loco.ps1 -Force
#   ./e2e-E3-loco.ps1 -ChatText "take a small step forward"
param([switch]$Force, [string]$ChatText = 'take a small step forward')

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E3: Chat -> Host -> UE loco (HARD-STOP GATE) ======'
Write-Output ('Host WS : ' + $script:HostWsUrl)
Write-Output ('UE WS   : ' + $script:UnrealWsUrl)
Write-Output ('ChatText: ' + $ChatText)

Assert-HostRecycled -Force:$Force

$h = Get-Health
if (-not $h) { Write-ResultLine 'E3' 'Fail' 'Host /health unreachable'; exit 1 }
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

    # 3. Send chat.send with loco intent
    $frame = New-Frame -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = 'e2e-E3' }
    Write-E2E 'send' $frame
    Send-WsFrame -Ws $hostWs -Json $frame

    # 4. Capture host reply
    $hostFrames = @()
    $captureHost = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $captureHost) {
        try { $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000; if ($r) { $hostFrames += $r; Write-E2E 'host-frame' $r } } catch { }
    }

    # 5. UE frames
    $ueFrames = @()
    if ($ueConnected) {
        $captureUe = [DateTime]::UtcNow.AddSeconds(6)
        while ([DateTime]::UtcNow -lt $captureUe) {
            try { $f = Receive-WsFrame -Ws $ueWs -TimeoutMs 1500; if ($f) { $ueFrames += $f; Write-E2E 'ue-frame' $f } } catch { }
        }
    }

    # 6. Evaluate: look for move_avatar_relative plain frame on UE
    $locoSeen = $false
    foreach ($f in $ueFrames) {
        if ($f -match '^move_avatar_relative\b') { $locoSeen = $true; break }
    }
    $evidence = "hostFrames=" + $hostFrames.Count + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; locoSeen=" + $locoSeen

    if ($locoSeen) {
        Write-ResultLine 'E3' 'Pass' $evidence
    } elseif (-not $ueConnected) {
        Write-ResultLine 'E3' 'Skip' ($evidence + ' (UE :8888 not running — needs UE up for loco wire verification)')
    } else {
        Write-ResultLine 'E3' 'Fail' ($evidence + ' HARD-STOP: do NOT enable SoulLoop if E3 fails')
    }
} catch {
    Write-ResultLine 'E3' 'Fail' ('Exception: ' + $_.Exception.Message + ' HARD-STOP: do NOT enable SoulLoop')
} finally {
    Close-Ws $hostWs
    Close-Ws $ueWs
}
