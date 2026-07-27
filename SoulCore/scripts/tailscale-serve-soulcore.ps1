<#
.SYNOPSIS
  Apply or tear down Tailscale serve proxies for SoulCore.Host :7700 (loopback).

.DESCRIPTION
  Tailnet-only exposure (no Funnel / no LAN bind). Matches docs/runbooks/tailscale-serve-soulcore.md.

  Default: enable TCP :7700 forward + HTTPS :8443 reverse proxy to http://127.0.0.1:7700

.PARAMETER Status
  Print tailscale serve status only.

.PARAMETER Off
  Disable the SoulCore serve endpoints created by this script (TCP 7700 + HTTPS 8443).
  Does not touch unrelated handlers (e.g. Ollama on :443).

.PARAMETER TcpOnly
  Only enable TCP forward on 7700.

.PARAMETER HttpsOnly
  Only enable HTTPS reverse proxy on 8443.
#>
[CmdletBinding()]
param(
    [switch]$Status,
    [switch]$Off,
    [switch]$TcpOnly,
    [switch]$HttpsOnly
)

$ErrorActionPreference = "Stop"

function Get-TailscaleExe {
    $cmd = Get-Command tailscale -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidate = Join-Path ${env:ProgramFiles} "Tailscale\tailscale.exe"
    if (Test-Path $candidate) { return $candidate }
    throw "Tailscale CLI not found. Install from https://tailscale.com/download/windows"
}

function Invoke-Tailscale {
    param([Parameter(Mandatory)][string[]]$CliArgs)
    & $script:Tailscale @CliArgs
    if ($LASTEXITCODE -ne 0) {
        throw "tailscale $($CliArgs -join ' ') failed with exit $LASTEXITCODE"
    }
}

$script:Tailscale = Get-TailscaleExe
Write-Host "Using: $script:Tailscale"

if ($Status) {
    Invoke-Tailscale -CliArgs @("serve", "status")
    exit 0
}

if ($Off) {
    Write-Host "Disabling SoulCore serve endpoints (TCP 7700, HTTPS 8443)..."
    & $script:Tailscale serve --tcp=7700 off 2>$null
    & $script:Tailscale serve --https=8443 off 2>$null
    Invoke-Tailscale -CliArgs @("serve", "status")
    exit 0
}

$doTcp = -not $HttpsOnly
$doHttps = -not $TcpOnly

# Probe local Host (advisory)
try {
    $health = Invoke-WebRequest -Uri "http://127.0.0.1:7700/health" -UseBasicParsing -TimeoutSec 3
    Write-Host "Local Host health: HTTP $($health.StatusCode)"
}
catch {
    Write-Warning "SoulCore Host not reachable at http://127.0.0.1:7700/health - start Host before phone clients connect."
}

if ($doTcp) {
    Write-Host "Enabling TCP serve :7700 -> 127.0.0.1:7700"
    Invoke-Tailscale -CliArgs @("serve", "--tcp=7700", "--bg", "--yes", "7700")
}

if ($doHttps) {
    Write-Host "Enabling HTTPS serve :8443 -> 127.0.0.1:7700"
    Invoke-Tailscale -CliArgs @("serve", "--https=8443", "--bg", "--yes", "7700")
}

Write-Host ""
Write-Host "Serve status:"
Invoke-Tailscale -CliArgs @("serve", "status")

$ip = (& $script:Tailscale ip -4 2>$null | Select-Object -First 1)
Write-Host ""
Write-Host "Phone examples (after AllowedHosts includes MagicDNS / TS IP):"
if ($doHttps) {
    Write-Host "  wss://<magicdns>:8443/ws"
    Write-Host "  https://<magicdns>:8443/health"
}
if ($doTcp -and $ip) {
    Write-Host "  ws://${ip}:7700/ws"
    Write-Host "  http://${ip}:7700/health"
}
Write-Host "See docs/runbooks/tailscale-serve-soulcore.md"

