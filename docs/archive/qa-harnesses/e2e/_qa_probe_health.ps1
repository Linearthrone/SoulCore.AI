# QA-01 F1/F4 probe script — read-only against soak Host
# Task 081
$ErrorActionPreference = 'Stop'

function Get-Health {
    try {
        $r = Invoke-RestMethod -Uri 'http://127.0.0.1:7700/health' -TimeoutSec 10
        return $r
    } catch {
        return @{ __error = $_.Exception.Message }
    }
}

Write-Output '===== C1+C3+C4+C5+C6: /health probe ====='
$h = Get-Health
$h | ConvertTo-Json -Depth 10

Write-Output ''
Write-Output '===== C2: WS endpoint accept-connection test ====='
# Connect only, do NOT send chat frames (soak-safe)
$wsResult = $null
try {
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $cts = [System.Threading.CancellationTokenSource]::new(8000)
    $t = $ws.ConnectAsync('ws://127.0.0.1:7700/ws', $cts.Token)
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    while (-not $t.IsCompleted -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
    }
    if ($t.IsCompleted) {
        $wsResult = @{ connected = $true; state = $ws.State.ToString() }
    } else {
        $cts.Cancel()
        $wsResult = @{ connected = $false; state = 'timeout' }
    }
    $ws.Dispose()
} catch {
    $wsResult = @{ connected = $false; state = 'error'; error = $_.Exception.Message }
}
$wsResult | ConvertTo-Json -Depth 5

Write-Output ''
Write-Output '===== F4: soak snapshot ====='
# Disk free
$disk = Get-PSDrive -Name C -ErrorAction SilentlyContinue
if ($disk) {
    Write-Output ('DiskFreeGB_C: ' + [math]::Round($disk.Free/1GB, 2))
    Write-Output ('DiskUsedGB_C: ' + [math]::Round($disk.Used/1GB, 2))
}

# Current time
Write-Output ('ProbeTime_UTC: ' + [DateTime]::UtcNow.ToString('o'))
Write-Output ('ProbeTime_Local: ' + (Get-Date).ToString('o'))
