# e2e-E1-speak.ps1
# E1 — Chat -> Host -> UE `speak` gate
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2 E1
#
# Verifies: a chat.send frame into Host WS triggers a `speak <text>` plain frame
# forwarded to UE :8888 (per UeVerbWireMapper.MapSpeak -> "speak <text>").
#
# !!! GUARD: Host must be recycled post-soak before running !!!
# The 24h soak (OPS-063, PID 47288) must be archived first. This script will
# refuse to run against a Host still in soak configuration unless -Force is given
# with explicit PM/OPS sign-off.
#
# Usage:
#   ./e2e-E1-speak.ps1                # guarded
#   ./e2e-E1-speak.ps1 -Force         # override guard (PM sign-off required)
#   ./e2e-E1-speak.ps1 -ChatText "hello from QA e2e"
param([switch]$Force, [string]$ChatText = 'QA E2E E1 speak probe')

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E1: Chat -> Host -> UE speak ======'
Write-Output ('Host WS : ' + $script:HostWsUrl)
Write-Output ('UE WS   : ' + $script:UnrealWsUrl)
Write-Output ('ChatText: ' + $ChatText)

Assert-HostRecycled -Force:$Force

# Pre-check Host health
$h = Get-Health
if (-not $h) { Write-ResultLine 'E1' 'Fail' 'Host /health unreachable'; exit 1 }
Write-Output ('Host health: status=' + $h.status + ' unreal.connected=' + $h.unreal.connected)

$hostWs = $null
$ueWs = $null
try {
    # 1. Connect UE listener FIRST (best-effort; UE may not be running during soak)
    $ueConnected = $false
    try {
        Write-E2E '' 'Connecting UE listener on :8888...'
        $ueWs = Connect-Ws -Url $script:UnrealWsUrl -TimeoutMs 5000
        $ueConnected = $true
        Write-E2E '' 'UE listener connected.'
    } catch {
        Write-E2E '' ('UE listener not reachable: ' + $_.Exception.Message)
    }

    # 2. Connect to Host WS
    Write-E2E '' 'Connecting Host WS...'
    $hostWs = Connect-Ws -Url $script:HostWsUrl -TimeoutMs 8000
    Write-E2E '' 'Host WS connected.'

    # 3. Send chat.send
    $frame = New-Frame -Type 'chat.send' -Payload @{ text = $ChatText; sessionId = 'e2e-E1' }
    Write-E2E 'send' $frame
    Send-WsFrame -Ws $hostWs -Json $frame

    # 4. Read Host response (chat.delta / chat.done / error)
    $hostReply = $null
    try { $hostReply = Receive-WsFrame -Ws $hostWs -TimeoutMs 10000 } catch { $hostReply = $null }
    Write-E2E 'host-reply' ($hostReply | Out-String)

    # 5. Read UE forwarded frame (if listener connected)
    $ueFrames = @()
    if ($ueConnected) {
        $captureDeadline = [DateTime]::UtcNow.AddSeconds(8)
        while ([DateTime]::UtcNow -lt $captureDeadline) {
            try {
                $f = Receive-WsFrame -Ws $ueWs -TimeoutMs 1500
                if ($f) { $ueFrames += $f; Write-E2E 'ue-frame' $f }
            } catch { }
        }
    }

    # 6. Evaluate
    $speakSeen = $false
    foreach ($f in $ueFrames) {
        if ($f -match '^speak\b') { $speakSeen = $true; break }
    }
    $evidence = "hostReply=" + (($hostReply -replace '\s+',' ')) + "; ueConnected=" + $ueConnected + "; ueFrames=" + $ueFrames.Count + "; speakSeen=" + $speakSeen
    if ($speakSeen) {
        Write-ResultLine 'E1' 'Pass' $evidence
    } elseif (-not $ueConnected) {
        Write-ResultLine 'E1' 'Skip' ($evidence + ' (UE :8888 not running — needs UE up for audible speak verification)')
    } else {
        Write-ResultLine 'E1' 'Fail' $evidence
    }
} catch {
    Write-ResultLine 'E1' 'Fail' ('Exception: ' + $_.Exception.Message)
} finally {
    Close-Ws $hostWs
    Close-Ws $ueWs
}
