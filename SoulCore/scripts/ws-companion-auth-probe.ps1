#Requires -Version 5.1
<#
.SYNOPSIS
  Probe SoulCore Host /ws companion auth (X-Api-Key preferred, Bearer parity).
.DESCRIPTION
  Confirms /health is up, then tries ClientWebSocket upgrades:
    - no auth
    - X-Api-Key (ChatDesktop path)
    - Authorization Bearer
    - wrong X-Api-Key
  Never prints the token — only length / present flags.
.EXAMPLE
  .\SoulCore\scripts\ws-companion-auth-probe.ps1
  .\SoulCore\scripts\ws-companion-auth-probe.ps1 -Port 7701
#>
[CmdletBinding()]
param(
    [string]$HostAddress = "127.0.0.1",
    [int]$Port = 7700,
    [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $EnvFile) {
    $EnvFile = Join-Path $RepoRoot "SoulCore\.env"
}

function Get-CompanionTokenFromEnvFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    foreach ($line in Get-Content -LiteralPath $Path -Encoding utf8) {
        $t = $line.Trim()
        if ($t.StartsWith("SOULCORE_COMPANION_API_TOKEN=")) {
            $v = $t.Substring("SOULCORE_COMPANION_API_TOKEN=".Length).Trim()
            if (($v.StartsWith('"') -and $v.EndsWith('"')) -or ($v.StartsWith("'") -and $v.EndsWith("'"))) {
                if ($v.Length -ge 2) { $v = $v.Substring(1, $v.Length - 2) }
            }
            if (-not [string]::IsNullOrWhiteSpace($v)) { return $v.Trim() }
        }
    }
    return $null
}

$token = $env:SOULCORE_COMPANION_API_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
    $token = Get-CompanionTokenFromEnvFile -Path $EnvFile
}
$tokenPresent = -not [string]::IsNullOrWhiteSpace($token)
$tokenLen = if ($tokenPresent) { $token.Length } else { 0 }

$healthUrl = "http://${HostAddress}:${Port}/health"
$wsUri = [Uri]"ws://${HostAddress}:${Port}/ws"
Write-Host "health=$healthUrl"
Write-Host "ws=$wsUri tokenPresent=$tokenPresent tokenLen=$tokenLen"

try {
    $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 5
    Write-Host "HEALTH ok status=$($health.status) port=$($health.port)"
} catch {
    Write-Error "HEALTH failed: $($_.Exception.Message). Start Host first."
    exit 2
}

function Connect-Ws {
    param(
        [string]$Label,
        [string]$HeaderName,
        [string]$HeaderValue
    )
    $ws = [System.Net.WebSockets.ClientWebSocket]::new()
    $cts = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(5))
    try {
        if ($HeaderName -and $HeaderValue) {
            $ws.Options.SetRequestHeader($HeaderName, $HeaderValue)
        }
        $ws.ConnectAsync($wsUri, $cts.Token).GetAwaiter().GetResult()
        $state = $ws.State.ToString()
        try { $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "probe", [Threading.CancellationToken]::None).Wait(2000) | Out-Null } catch {}
        Write-Host ("{0,-14} => CONNECTED state={1} header={2}" -f $Label, $state, $(if ($HeaderName) { $HeaderName } else { "none" }))
        return $true
    } catch {
        $msg = $_.Exception.Message
        if ($_.Exception.InnerException) { $msg = $_.Exception.InnerException.Message }
        Write-Host ("{0,-14} => FAIL header={1} {2}" -f $Label, $(if ($HeaderName) { $HeaderName } else { "none" }), $msg)
        return $false
    } finally {
        $cts.Dispose()
        $ws.Dispose()
    }
}

$noAuth = Connect-Ws -Label "NO_AUTH" -HeaderName "" -HeaderValue ""
$apiOk = $false
$bearerOk = $false
if ($tokenPresent) {
    $apiOk = Connect-Ws -Label "X_API_KEY" -HeaderName "X-Api-Key" -HeaderValue $token
    $bearerOk = Connect-Ws -Label "BEARER" -HeaderName "Authorization" -HeaderValue ("Bearer " + $token)
    [void](Connect-Ws -Label "X_API_KEY_BAD" -HeaderName "X-Api-Key" -HeaderValue "wrong-token-value-not-matching!!!!")
} else {
    Write-Warning "No SOULCORE_COMPANION_API_TOKEN in process env or $EnvFile — skipped positive auth cases."
}

# Exit: success when Host requires auth ⇒ X-Api-Key connects; or Host open ⇒ no-auth connects.
if ($tokenPresent) {
    if ($apiOk) {
        Write-Host "RESULT=PASS desktop path X-Api-Key OK (Bearer parity=$bearerOk noAuth=$noAuth)"
        exit 0
    }
    Write-Host "RESULT=FAIL X-Api-Key did not connect (tokenPresent=true tokenLen=$tokenLen)"
    exit 1
}

if ($noAuth) {
    Write-Host "RESULT=PASS open gate (no companion token configured on client; confirm Host gate)"
    exit 0
}
Write-Host "RESULT=FAIL no token locally and no-auth upgrade failed (Host likely requires token)"
exit 1
