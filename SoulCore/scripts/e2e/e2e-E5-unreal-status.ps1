# e2e-E5-unreal-status.ps1
# E5 — Presence Unreal status surface shows bridge target
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2 E5
#
# Verifies: /health exposes unreal.target (ws://127.0.0.1:8888) and the
# unreal.connected flag is present so Presence can surface bridge status.
# Also sends a ping to solicit a presence.status frame on the WS.
#
# !!! GUARD: Host must be recycled post-soak before running !!!
#
# Usage:
#   ./e2e-E5-unreal-status.ps1
#   ./e2e-E5-unreal-status.ps1 -Force
param([switch]$Force)

. "$PSScriptRoot/e2e-harness-common.ps1"

Write-Output '====== E5: Presence Unreal status surface ======'
Write-Output ('Host   : ' + $script:HostUrl)
Write-Output ('Host WS: ' + $script:HostWsUrl)

Assert-HostRecycled -Force:$Force

$h = Get-Health
if (-not $h) { Write-ResultLine 'E5' 'Fail' 'Host /health unreachable'; exit 1 }

# 1. /health unreal block
$unrealBlock = $h.unreal
Write-Output ('/health.unreal: ' + ($unrealBlock | ConvertTo-Json -Compress))
$targetPresent = (-not [string]::IsNullOrWhiteSpace($unrealBlock.target))
$connectedFlag = ($null -ne $unrealBlock.connected)
$enabledFlag = ($null -ne $unrealBlock.enabled)

# 2. Solicit a presence.status frame on WS
$hostWs = $null
$presenceStatus = $false
try {
    $hostWs = Connect-Ws -Url $script:HostWsUrl -TimeoutMs 8000
    $ping = New-Frame -Type 'ping' -Payload @{}
    Send-WsFrame -Ws $hostWs -Json $ping
    $deadline = [DateTime]::UtcNow.AddSeconds(6)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $r = Receive-WsFrame -Ws $hostWs -TimeoutMs 2000
            if ($r) { Write-E2E 'frame' $r; if ($r -match '"presence.status"') { $presenceStatus = $true } }
        } catch { }
    }
} catch {
    Write-E2E '' ('WS probe error: ' + $_.Exception.Message)
} finally {
    Close-Ws $hostWs
}

$evidence = "unreal.target=" + $unrealBlock.target + "; unreal.enabled=" + $unrealBlock.enabled + "; unreal.connected=" + $unrealBlock.connected + "; presenceStatusFrame=" + $presenceStatus
if ($targetPresent -and $enabledFlag -and $connectedFlag) {
    Write-ResultLine 'E5' 'Pass' $evidence
} else {
    Write-ResultLine 'E5' 'Fail' ($evidence + ' (unreal block incomplete in /health)')
}
