#Requires -Version 5.1
<#
.SYNOPSIS
  Apply or tear down Tailscale serve proxies for SoulCore.Host :7720 (loopback),
  and sync AllowedHosts so the Host actually accepts Tailscale-served requests.

.DESCRIPTION
  Tailnet-only exposure (no Funnel / no LAN bind). Matches docs/runbooks/tailscale-serve-soulcore.md.

  Default: sync AllowedHosts, enable TCP :7720 forward + HTTPS :8453 reverse proxy to http://127.0.0.1:7720.
  -Off removes only those two endpoints. It does not turn off 7700, 8443, or 8444.

  AllowedHosts sync (always on unless -SkipAllowedHosts):
    * Resolves Tailscale IPv4 (tailscale ip -4) + MagicDNS name (tailscale status --json).
    * Reads SoulCore/SoulCore.Host/appsettings.json "AllowedHosts".
    * Adds the MagicDNS name (no trailing dot) and Tailscale IPv4 if not already present.
    * Writes back with UTF-8 (no BOM), preserving the rest of the JSON.
    * Restart Host after editing (ALLSTART calls this BEFORE starting Host).

.PARAMETER Status
  Print tailscale serve status only.

.PARAMETER Off
  Disable the SoulCore serve endpoints created by this script (TCP 7720 + HTTPS 8453).
  Does not touch unrelated handlers (7700, 8443, 8444, or Ollama on :443).

.PARAMETER TcpOnly
  Only enable TCP forward on -TcpPort (default 7720). No HTTPS reverse proxy, no AllowedHosts sync.

.PARAMETER HttpsOnly
  Only enable HTTPS reverse proxy on -HttpsPort (default 8453). No TCP forward.

.PARAMETER TcpPort
  Tailscale TCP listen port. Default 7720.

.PARAMETER HttpsPort
  Tailscale HTTPS listen port. Default 8453.

.PARAMETER TargetPort
  Loopback Host port the proxies forward to. Default 7720.

.PARAMETER SyncAllowedHostsOnly
  Only run the AllowedHosts sync; do not change tailscale serve state.
  Use this from ALLSTART before Host start.

.PARAMETER SkipAllowedHosts
  Do not touch appsettings.json (caller already did, or not needed).

.EXAMPLE
  .\tailscale-serve-soulcore.ps1                         # apply A+B + sync hosts
  .\tailscale-serve-soulcore.ps1 -SyncAllowedHostsOnly   # sync hosts only
  .\tailscale-serve-soulcore.ps1 -Status                 # show serve status
  .\tailscale-serve-soulcore.ps1 -Off                    # remove A+B only
#>
[CmdletBinding()]
param(
    [switch]$Status,
    [switch]$Off,
    [switch]$TcpOnly,
    [switch]$HttpsOnly,
    [switch]$SyncAllowedHostsOnly,
    [switch]$SkipAllowedHosts,
    [int]$TcpPort = 7720,
    [int]$HttpsPort = 8453,
    [int]$TargetPort = 7720
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$AppSettings = Join-Path $RepoRoot "SoulCore\SoulCore.Host\appsettings.json"

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

function Get-TailscaleIdentity {
    # IPv4 (first line)
    $ip = (& $script:Tailscale ip -4 2>$null | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($ip)) { $ip = $null }

    # MagicDNS via status --json (strip trailing dot)
    $dns = $null
    try {
        $json = (& $script:Tailscale status --json 2>$null) -join "`n"
        if ($json) {
            $obj = $json | ConvertFrom-Json -ErrorAction Stop
            if ($obj.Self -and $obj.Self.DNSName) {
                $dns = ([string]$obj.Self.DNSName).TrimEnd('.')
            }
        }
    } catch {
        Write-Warning "Could not parse 'tailscale status --json' for MagicDNS: $($_.Exception.Message)"
    }

    return @{ Ip = $ip; Dns = $dns }
}

function Sync-AllowedHosts {
    param(
        [Parameter(Mandatory)][string]$Path,
        [hashtable]$Identity
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Warning "appsettings.json not found at $Path - skipping AllowedHosts sync"
        return $false
    }

    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8

    # Detect BOM from raw bytes (Get-Content -Raw strips the BOM from the string).
    $hasBom = $false
    try {
        $probe = [System.IO.File]::ReadAllBytes($Path)
        if ($probe.Length -ge 3 -and $probe[0] -eq 0xEF -and $probe[1] -eq 0xBB -and $probe[2] -eq 0xBF) {
            $hasBom = $true
        }
    } catch { }

    # Parse just to read the current value (do not round-trip via ConvertTo-Json,
    # which would reformat the whole file and create a noisy diff).
    try {
        $cfg = $raw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        Write-Warning "Could not parse $Path as JSON - skipping AllowedHosts sync"
        return $false
    }

    $current = [string]$cfg.AllowedHosts
    if ([string]::IsNullOrWhiteSpace($current)) {
        Write-Warning "AllowedHosts empty in $Path - skipping (set it first)"
        return $false
    }

    $hosts = New-Object System.Collections.Generic.List[string]
    foreach ($h in $current -split ';') {
        $trimmed = $h.Trim()
        if ($trimmed) { $hosts.Add($trimmed) }
    }

    $added = @()
    if ($Identity.Ip) {
        if (-not ($hosts -contains $Identity.Ip)) {
            $hosts.Add($Identity.Ip)
            $added += $Identity.Ip
        }
    } else {
        Write-Warning "No Tailscale IPv4 resolved - not adding one to AllowedHosts"
    }
    if ($Identity.Dns) {
        if (-not ($hosts -contains $Identity.Dns)) {
            $hosts.Add($Identity.Dns)
            $added += $Identity.Dns
        }
    } else {
        Write-Warning "No Tailscale MagicDNS name resolved - not adding one to AllowedHosts"
    }

    if ($added.Count -eq 0) {
        Write-Host "AllowedHosts already includes Tailscale IP/DNS ($($hosts -join '; '))."
        return $false
    }

    $newVal = ($hosts -join ';')
    # Escape for JSON string: backslash and double-quote.
    $newValJson = $newVal.Replace('\','\\').Replace('"','\"')

    # Patch only the "AllowedHosts": "..." value, preserving everything else
    # (indentation, key order, trailing newline, etc.). Handles the key with
    # arbitrary surrounding whitespace as emitted by ConvertTo-Json / manual edits.
    $pattern = '(?s)("AllowedHosts"\s*:\s*")[^"]*(")'
    if (-not ($raw -match $pattern)) {
        Write-Warning "Could not locate 'AllowedHosts' key in $Path - skipping"
        return $false
    }
    $patched = [regex]::Replace($raw, $pattern, "`${1}${newValJson}`${2}")

    try {
        # Preserve original BOM state (don't add or strip one).
        $enc = New-Object System.Text.UTF8Encoding($hasBom)
        [System.IO.File]::WriteAllText($Path, $patched, $enc)
    } catch {
        Write-Warning "Failed to write $Path - $($_.Exception.Message)"
        return $false
    }

    Write-Host "AllowedHosts updated. Added: $($added -join ', ')"
    Write-Host "  -> $newVal"
    Write-Host "Restart SoulCore.Host for the change to take effect."
    return $true
}

$script:Tailscale = Get-TailscaleExe
Write-Host "Using: $script:Tailscale"

# ---- Status-only short-circuit ----
if ($Status) {
    Invoke-Tailscale -CliArgs @("serve", "status")
    exit 0
}

# ---- Off (tear down) ----
if ($Off) {
    Write-Host "Disabling SoulCore serve endpoints (TCP $TcpPort, HTTPS $HttpsPort)..."
    # Already-off is success: Tailscale prints "serve config does not exist" and
    # PowerShell may treat stderr as a native error even with 2>$null.
    # Only these two ports. Never 7700, 8443, or 8444.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    foreach ($argsOff in @(
        @("serve", "--tcp=$TcpPort", "off"),
        @("serve", "--https=$HttpsPort", "off")
    )) {
        $err = & $script:Tailscale @argsOff 2>&1
        if ($LASTEXITCODE -ne 0) {
            $msg = ($err | Out-String).Trim()
            if ($msg -match 'does not exist|no serve|not (found|configured)') {
                Write-Host "  already off: $($argsOff -join ' ')"
            } else {
                Write-Warning "tailscale $($argsOff -join ' ') failed: $msg"
            }
        }
    }
    $ErrorActionPreference = $prevEap
    Invoke-Tailscale -CliArgs @("serve", "status")
    exit 0
}

# ---- Resolve identity once (used by both AllowedHosts sync and the URL hints) ----
$identity = Get-TailscaleIdentity

# ---- AllowedHosts sync ----
$hostsChanged = $false
if (-not $SkipAllowedHosts) {
    Write-Host "=== Sync AllowedHosts (Tailscale IP/MagicDNS) ==="
    try {
        $hostsChanged = Sync-AllowedHosts -Path $AppSettings -Identity $identity
    } catch {
        Write-Warning "AllowedHosts sync failed - $($_.Exception.Message)"
    }
} else {
    Write-Host "AllowedHosts sync skipped (-SkipAllowedHosts)."
}

# ---- Sync-only short-circuit ----
if ($SyncAllowedHostsOnly) {
    if ($identity.Ip)  { Write-Host "Tailscale IPv4:   $($identity.Ip)" }
    if ($identity.Dns) { Write-Host "Tailscale MagicDNS: $($identity.Dns)" }
    exit 0
}

# ---- Enable serve ----
$doTcp = -not $HttpsOnly
$doHttps = -not $TcpOnly

# Probe local Host (advisory)
try {
    $health = Invoke-WebRequest -Uri "http://127.0.0.1:${TargetPort}/health" -UseBasicParsing -TimeoutSec 3
    Write-Host "Local Host health: HTTP $($health.StatusCode)"
}
catch {
    Write-Warning "SoulCore Host not reachable at http://127.0.0.1:${TargetPort}/health - start Host before phone clients connect."
}

if ($doTcp) {
    Write-Host "Enabling TCP serve :$TcpPort -> 127.0.0.1:$TargetPort"
    Invoke-Tailscale -CliArgs @("serve", "--tcp=$TcpPort", "--bg", "--yes", "$TargetPort")
}

if ($doHttps) {
    Write-Host "Enabling HTTPS serve :$HttpsPort -> 127.0.0.1:$TargetPort"
    Invoke-Tailscale -CliArgs @("serve", "--https=$HttpsPort", "--bg", "--yes", "$TargetPort")
}

Write-Host ""
Write-Host "Serve status:"
Invoke-Tailscale -CliArgs @("serve", "status")

Write-Host ""
Write-Host "Phone examples (after AllowedHosts includes MagicDNS / TS IP):"
if ($doHttps -and $identity.Dns) {
    Write-Host "  wss://$($identity.Dns):${HttpsPort}/ws"
    Write-Host "  https://$($identity.Dns):${HttpsPort}/health"
}
if ($doTcp -and $identity.Ip) {
    Write-Host "  ws://$($identity.Ip):${TcpPort}/ws"
    Write-Host "  http://$($identity.Ip):${TcpPort}/health"
}
Write-Host "See docs/runbooks/tailscale-serve-soulcore.md"
