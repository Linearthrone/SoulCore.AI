# e2e-E4-emotion-strip.ps1
# E4 — Presence emotion strip + user correction post-recycle
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2 E4
#
# Verifies:
#   (a) Host emits emotion.snapshot frames (Presence emotion strip source)
#   (b) Client can send emotion.correct and Host echoes back a revised emotion.snapshot
#
# !!! GUARD: Host must be recycled post-soak before running !!!
#
# Usage:
#   ./e2e-E4-emotion-strip.ps1
#   ./e2e-E4-emotion-strip.ps1 -Force
param([switch]$Force)

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E4: Presence emotion strip + correction ======'
Write-Output ('Host WS : ' + $script:HostWsUrl)

Assert-HostRecycled -Force:$Force

$h = Get-Health
if (-not $h) { Write-ResultLine 'E4' 'Fail' 'Host /health unreachable'; exit 1 }
Write-Output ('Host health: status=' + $h.status + ' soulLoop.enabled=' + $h.soulLoop.enabled)

$hostWs = $null
try {
    $hostWs = Connect-Ws -Url $script:HostWsUrl -TimeoutMs 8000
    Write-E2E '' 'Host WS connected.'

    # 1. Send a ping to solicit a presence.status / emotion.snapshot broadcast
    $ping = New-Frame -Type 'ping' -Payload @{}
    Send-WsFrame -Ws $hostWs -Json $ping
    Write-E2E 'send' 'ping'

    # 2. Collect frames for a few seconds looking for emotion.snapshot
    $frames = @()
    $snapshotSeen = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000
            if ($r) { $frames += $r; Write-E2E 'frame' $r; if ($r -match '"emotion.snapshot"') { $snapshotSeen = $true } }
        } catch { }
    }

    # 3. Send emotion.correct (user correction of felt emotion)
    $correct = New-Frame -Type 'emotion.correct' -Payload @{ valence = -0.4; arousal = 0.7; dominance = 0.3; focus = 0.5; note = 'E2E correction probe' }
    Write-E2E 'send' $correct
    Send-WsFrame -Ws $hostWs -Json $correct

    # 4. Look for a revised emotion.snapshot echo
    $correctedSnapshot = $false
    $deadline2 = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $deadline2) {
        try {
            $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000
            if ($r) { $frames += $r; Write-E2E 'frame' $r; if ($r -match '"emotion.snapshot"') { $correctedSnapshot = $true } }
        } catch { }
    }

    $evidence = "frames=" + $frames.Count + "; initialSnapshot=" + $snapshotSeen + "; correctedSnapshot=" + $correctedSnapshot
    if ($snapshotSeen -and $correctedSnapshot) {
        Write-ResultLine 'E4' 'Pass' $evidence
    } elseif (-not $snapshotSeen) {
        Write-ResultLine 'E4' 'Fail' ($evidence + ' (no initial emotion.snapshot broadcast)')
    } else {
        Write-ResultLine 'E4' 'Fail' ($evidence + ' (emotion.correct did not produce revised snapshot)')
    }
} catch {
    Write-ResultLine 'E4' 'Fail' ('Exception: ' + $_.Exception.Message)
} finally {
    Close-Ws $hostWs
}
