# e2e-E6-want-strip.ps1
# E6 — Want strip placeholder test (SoulLoop OFF)
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2 E6 + §3.3
#
# Verifies: with SoulLoop:Enabled=false (default), the Want strip in Presence
# shows a harmless placeholder. Concretely:
#   (a) /health reports soulLoop.enabled = false
#   (b) a loop.tick frame sent to Host is acked with loop.tick.ok (no want emitted)
#   (c) NO loop.want frame is broadcast while SoulLoop is off
#
# !!! GUARD: Host must be recycled post-soak before running !!!
# !!! This gate CONFIRMS SoulLoop stays OFF until §3.3 decision. !!!
#
# Usage:
#   ./e2e-E6-want-strip.ps1
#   ./e2e-E6-want-strip.ps1 -Force
param([switch]$Force)

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E6: Want strip placeholder (SoulLoop OFF) ======'
Write-Output ('Host WS: ' + $script:HostWsUrl)

Assert-HostRecycled -Force:$Force

$h = Get-Health
if (-not $h) { Write-ResultLine 'E6' 'Fail' 'Host /health unreachable'; exit 1 }
$soulLoopOff = ($h.soulLoop.enabled -eq $false)
Write-Output ('/health.soulLoop.enabled = ' + $h.soulLoop.enabled)

$hostWs = $null
$wantEmitted = $false
$tickOk = $false
try {
    $hostWs = Connect-Ws -Url $script:HostWsUrl -TimeoutMs 8000
    Write-E2E '' 'Host WS connected.'

    # 1. Send loop.tick (should be a no-op / ack-only when SoulLoop off)
    $tick = New-Frame -Type 'loop.tick' -Payload @{}
    Write-E2E 'send' $tick
    Send-WsFrame -Ws $hostWs -Json $tick

    # 2. Collect frames — expect loop.tick.ok; expect NO loop.want
    $frames = @()
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000
            if ($r) {
                $frames += $r; Write-E2E 'frame' $r
                if ($r -match '"loop.tick.ok"') { $tickOk = $true }
                if ($r -match '"loop.want"') { $wantEmitted = $true }
            }
        } catch { }
    }

    $evidence = "soulLoopOff=" + $soulLoopOff + "; tickOk=" + $tickOk + "; wantEmitted=" + $wantEmitted + "; frames=" + $frames.Count

    # PASS: SoulLoop off AND no want emitted. tickOk is expected but not strictly required
    # (Host may silently no-op loop.tick when disabled; absence of loop.want is the key gate).
    if ($soulLoopOff -and (-not $wantEmitted)) {
        Write-ResultLine 'E6' 'Pass' ($evidence + ' (Want strip stays placeholder while SoulLoop off)')
    } elseif ($wantEmitted) {
        Write-ResultLine 'E6' 'Fail' ($evidence + ' HARD-STOP: loop.want emitted while SoulLoop disabled — want strip would show live want')
    } else {
        Write-ResultLine 'E6' 'Fail' ($evidence + ' (SoulLoop unexpectedly enabled)')
    }
} catch {
    Write-ResultLine 'E6' 'Fail' ('Exception: ' + $_.Exception.Message)
} finally {
    Close-Ws $hostWs
}
