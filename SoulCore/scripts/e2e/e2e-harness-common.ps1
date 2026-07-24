# e2e-harness-common.ps1
# Shared helpers for SoulCore E2E harness scripts (E1-E6).
# Charter ref: charter-lock-and-cutover-weekend-checklist.md §3.2
#
# GUARD: These scripts are STAGED, not executed. They require a Host recycle
# post-soak before running. The 24h soak (OPS-063, PID 47288) must finish first.
#
# Usage: dot-source this from each e2e-E*.ps1 script.
#   . ./e2e-harness-common.ps1

$ErrorActionPreference = 'Stop'

# --- Config (overridable via env / params at call site) ---
if (-not $script:HostUrl)     { $script:HostUrl    = $env:SOULCORE_E2E_HOST_URL    ; if (-not $script:HostUrl)    { $script:HostUrl    = 'http://127.0.0.1:7700' } }
if (-not $script:HostWsUrl)   { $script:HostWsUrl  = $env:SOULCORE_E2E_HOST_WS_URL; if (-not $script:HostWsUrl)  { $script:HostWsUrl  = 'ws://127.0.0.1:7700/ws' } }
if (-not $script:UnrealWsUrl) { $script:UnrealWsUrl = $env:SOULCORE_E2E_UE_WS_URL ; if (-not $script:UnrealWsUrl){ $script:UnrealWsUrl = 'ws://127.0.0.1:8888' } }
if (-not $script:TimeoutSec)  { $script:TimeoutSec = 15 }

function Write-E2E([string]$label, [string]$msg) {
    Write-Output ("[{0}] {1}" -f (Get-Date).ToString('HH:mm:ss.fff'), $msg)
    if ($label) { Write-Output ("  >> {0}: {1}" -f $label, $msg) }
}

function Assert-HostRecycled {
    # Hard guard: refuse to run against a Host still in soak phase.
    # Caller may bypass with -Force if they have explicit PM/OPS sign-off.
    param([switch]$Force)
    if ($Force) { return $true }
    try {
        $h = Invoke-RestMethod -Uri "$script:HostUrl/health" -TimeoutSec 5
        # Heuristic: soak Host reports phase 1 and soulLoop false. After recycle, phase may bump.
        # The real guard is the operator confirming the soak is archived; this is a soft check.
        if ($h.soulLoop.enabled -eq $false -and $h.phase -le 1) {
            Write-Output 'GUARD: Host appears to still be in soak configuration (soulLoop=false, phase<=1).'
            Write-Output 'GUARD: Host must be recycled post-soak before running E2E. Aborting (use -Force to override with PM sign-off).'
            exit 2
        }
    } catch {
        Write-Output ('GUARD: cannot reach Host /health: ' + $_.Exception.Message)
        Write-Output 'GUARD: Host must be recycled and healthy before running E2E. Aborting.'
        exit 2
    }
    return $true
}

function New-Frame {
    param(
        [Parameter(Mandatory)][string]$Type,
        [object]$Payload = $null,
        [string]$Id = ''
    )
    if (-not $Id) { $Id = [Guid]::NewGuid().ToString('N') }
    $ts = [DateTimeOffset]::UtcNow.ToString('O')
    if ($null -eq $Payload) {
        $payloadJson = 'null'
    } else {
        $payloadJson = $Payload | ConvertTo-Json -Compress -Depth 10
    }
    return @{ v = 1; type = $Type; id = $Id; ts = $ts; payload = $Payload } | ConvertTo-Json -Compress -Depth 10
}

function Connect-Ws {
    param([Parameter(Mandatory)][string]$Url, [int]$TimeoutMs = 8000)
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $t = $ws.ConnectAsync($Url, $cts.Token)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while (-not $t.IsCompleted -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 50
    }
    if (-not $t.IsCompleted) {
        $cts.Cancel()
        $ws.Dispose()
        throw "Connect timeout to $Url"
    }
    if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.Dispose()
        throw "Connect failed to $Url (state=$($ws.State))"
    }
    return $ws
}

function Send-WsFrame {
    param(
        [Parameter(Mandatory)]$Ws,
        [Parameter(Mandatory)][string]$Json
    )
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
    $seg = [ArraySegment[byte]]::new($bytes)
    $cts = [System.Threading.CancellationTokenSource]::new(5000)
    $Ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token) | Out-Null
}

function Receive-WsFrame {
    param(
        [Parameter(Mandatory)]$Ws,
        [int]$TimeoutMs = 8000
    )
    $buf = New-Object byte[] 16384
    $seg = [ArraySegment[byte]]::new($buf)
    $cts = [System.Threading.CancellationTokenSource]::new($TimeoutMs)
    $sb = New-Object System.Text.StringBuilder
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    do {
        $r = $Ws.ReceiveAsync($seg, $cts.Token)
        while (-not $r.IsCompleted -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 20 }
        if (-not $r.IsCompleted) { throw 'Receive timeout' }
        $null = $sb.Append([System.Text.Encoding]::UTF8.GetString($buf, 0, $r.Result.Count))
        if ($r.Result.EndOfMessage) { break }
    } while ($true)
    return $sb.ToString()
}

function Close-Ws {
    param($Ws)
    if ($Ws -and $Ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try {
            $cts = [System.Threading.CancellationTokenSource]::new(2000)
            $Ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'bye', $cts.Token) | Out-Null
        } catch { }
    }
    if ($Ws) { $Ws.Dispose() }
}

function Get-Health {
    try { return Invoke-RestMethod -Uri "$script:HostUrl/health" -TimeoutSec 5 } catch { return $null }
}

# Capture UE-side verb frame as a listener on UE :8888.
# Returns a hashtable with capturedFrames (array of raw strings) and connected flag.
function Start-UeListener {
    param([int]$CaptureMs = 8000)
    $result = @{ connected = $false; frames = @(); error = $null }
    try {
        $ue = Connect-Ws -Url $script:UnrealWsUrl -TimeoutMs 5000
        $result.connected = $true
        $deadline = [DateTime]::UtcNow.AddMilliseconds($CaptureMs)
        while ([DateTime]::UtcNow -lt $deadline) {
            try {
                $frame = Receive-WsFrame -Ws $ue -TimeoutMs 1000
                if ($frame) { $result.frames += $frame }
            } catch {
                # timeout on a single recv is fine; keep looping until capture window ends
            }
        }
        Close-Ws $ue
    } catch {
        $result.error = $_.Exception.Message
    }
    return $result
}

function Write-ResultLine {
    param([string]$Gate, [string]$Result, [string]$Evidence)
    Write-Output ''
    Write-Output ('===== {0} RESULT =====' -f $Gate)
    Write-Output ('Result:   {0}' -f $Result)
    Write-Output ('Evidence: {0}' -f $Evidence)
    Write-Output ('=================================')
}

Write-Output 'e2e-harness-common loaded.'
