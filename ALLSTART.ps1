#Requires -Version 5.1
<#
.SYNOPSIS
  Start local SoulCore.Host (Victoria), wait until healthy, then launch House.ChatDesktop.
.DESCRIPTION
  Starts Host (Ollama tool-loop + desktop tools). Opens Chrome/sites via desktop_open_app.
  Refuses to attach the GUI to a foreign :7700 occupant (e.g. Cursor cloud port-forward to a
  Linux/ubuntu Host). If 7700 is stolen, starts local Host on -AlternatePort and points the GUI
  there via HOUSE_SOULCORE_PORT.

  OPS-179: child scripts run with timeouts so Tailscale / Voice health probes cannot freeze forever.
.EXAMPLE
  .\ALLSTART.ps1
  .\ALLSTART.ps1 -SkipPreflight
  .\ALLSTART.ps1 -SkipVoice
  .\ALLSTART.ps1 -Configuration Debug
  .\ALLSTART.ps1 -RestartHost
#>
[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [int]$Port = 7700,
    [int]$AlternatePort = 7701,
    [switch]$SkipPreflight,
    [switch]$ForceRebuild,
    [int]$HealthTimeoutSec = 45,
    [switch]$SkipTailscaleServe,
    [switch]$SkipVoice,
    [switch]$SkipBrowserBridge,
    [int]$HostStartTimeoutSec = 180,
    [int]$TailscaleTimeoutSec = 30,
    [int]$VoiceTimeoutSec = 45,
    [int]$BrowserBridgeTimeoutSec = 45,
    # Kill existing local Host and start fresh (reloads SoulCore/.env guest password).
    [switch]$RestartHost
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$StartHost = Join-Path $RepoRoot "SoulCore\scripts\start-soulcore.ps1"
$StartBrowserBridge = Join-Path $RepoRoot "SoulCore\scripts\start-browser-bridge.ps1"
$StartGui = Join-Path $RepoRoot "start-desktopgui.ps1"
$TailscaleServe = Join-Path $RepoRoot "SoulCore\scripts\tailscale-serve-soulcore.ps1"

if (-not (Test-Path -LiteralPath $StartHost)) {
    throw "Missing Host start script: $StartHost"
}
if (-not (Test-Path -LiteralPath $StartGui)) {
    throw "Missing desktop GUI script: $StartGui"
}

function Get-HealthObject {
    param([int]$LocalPort)
    try {
        return Invoke-RestMethod -Uri "http://127.0.0.1:${LocalPort}/health" -TimeoutSec 3 -ErrorAction Stop
    } catch {
        return $null
    }
}

function Test-LocalVictoriaHealth {
    param($Health)
    if ($null -eq $Health) { return $false }
    if ("$($Health.service)" -ne "SoulCore.Host") { return $false }
    $path = [string]$Health.memory.path
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }

    # Canonical Windows Victoria memory
    $expectedRoot = Join-Path $env:LOCALAPPDATA "SoulCore"
    if ($path.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($path -match '(?i)\\AppData\\Local\\SoulCore\\') { return $true }

    # Cloud / Linux agent Host - not this machine's Victoria
    if ($path -match '^/home/' -or $path -match '/\.local/share/SoulCore') { return $false }
    if ($path -match '(?i)/ubuntu/') { return $false }

    return $false
}

# OPS-179: never Start-Process -Wait without a deadline.
function Invoke-ScriptWithTimeout {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][int]$TimeoutSec
    )
    Write-Host "[$Label] starting (timeout ${TimeoutSec}s) ..."
    $proc = Start-Process -FilePath "powershell.exe" `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory `
        -PassThru -NoNewWindow
    if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
        Write-Warning "[$Label] timed out after ${TimeoutSec}s - stopping child PID $($proc.Id)"
        try {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            # Tree-kill common leftover powershell kids (best-effort).
            Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
                Where-Object { $_.ParentProcessId -eq $proc.Id } |
                ForEach-Object {
                    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
                }
        } catch { }
        return @{ TimedOut = $true; ExitCode = -1 }
    }
    # Timed WaitForExit can leave ExitCode $null until a final WaitForExit().
    $proc.Refresh()
    if (-not $proc.HasExited) {
        $null = $proc.WaitForExit(5000)
    } else {
        $null = $proc.WaitForExit()
    }
    $code = $proc.ExitCode
    if ($null -eq $code) { $code = 0 }
    return @{ TimedOut = $false; ExitCode = [int]$code }
}

function Start-LocalSoulCore {
    param([int]$LocalPort)
    $hostArgList = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $StartHost,
        "-Port", "$LocalPort"
    )
    if ($SkipPreflight) { $hostArgList += "-SkipPreflight" }
    if ($ForceRebuild) { $hostArgList += "-ForceRebuild" }
    if ($RestartHost) { $hostArgList += "-RestartHost" }

    $result = Invoke-ScriptWithTimeout `
        -Label "start-soulcore :$LocalPort" `
        -ArgumentList $hostArgList `
        -WorkingDirectory $RepoRoot `
        -TimeoutSec $HostStartTimeoutSec
    if ($result.TimedOut) {
        # Host may still have come up before the wrapper timed out.
        if (Test-LocalVictoriaHealth -Health (Get-HealthObject -LocalPort $LocalPort)) {
            Write-Warning "start-soulcore.ps1 timed out, but local Victoria is healthy on :$LocalPort - continuing"
            return
        }
        throw "start-soulcore.ps1 timed out after ${HostStartTimeoutSec}s on port $LocalPort"
    }
    if ($result.ExitCode -ne 0) {
        if (Test-LocalVictoriaHealth -Health (Get-HealthObject -LocalPort $LocalPort)) {
            Write-Warning "start-soulcore.ps1 exit $($result.ExitCode), but local Victoria is healthy on :$LocalPort - continuing"
            return
        }
        throw "start-soulcore.ps1 failed on port $LocalPort (exit $($result.ExitCode))"
    }
}

function Wait-LocalVictoria {
    param([int]$LocalPort, [int]$TimeoutSec)
    $url = "http://127.0.0.1:${LocalPort}/health"
    Write-Host "Waiting for local Victoria Host at $url ..."
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
    while ([DateTime]::UtcNow -lt $deadline) {
        $h = Get-HealthObject -LocalPort $LocalPort
        if (Test-LocalVictoriaHealth -Health $h) {
            Write-Host "Local Victoria Host healthy."
            Write-Host "  memory: $($h.memory.path)"
            if ($null -ne $h.inference.model) {
                Write-Host "  model:  $($h.inference.model)"
            }
            return $h
        }
        Start-Sleep -Milliseconds 400
    }
    throw "Local Victoria Host did not become healthy within ${TimeoutSec}s ($url)"
}

function Test-OllamaChatModel {
    param(
        [string]$Model = "qwen2.5:14b",
        [string]$OllamaUrl = "http://127.0.0.1:11434"
    )
    try {
        $tagsUrl = $OllamaUrl.TrimEnd('/') + "/api/tags"
        $tags = Invoke-RestMethod -Uri $tagsUrl -TimeoutSec 5 -ErrorAction Stop
        $names = @()
        if ($null -ne $tags.models) {
            $names = @($tags.models | ForEach-Object { $_.name })
        }
        $found = $false
        foreach ($n in $names) {
            if ([string]::IsNullOrWhiteSpace($n)) { continue }
            if ($n -eq $Model -or $n -eq ($Model + ":latest") -or $n.StartsWith($Model + ":")) {
                $found = $true
                break
            }
        }
        if ($found) {
            Write-Host "Ollama chat model OK: $Model"
            return $true
        }
        Write-Warning "Ollama chat model '$Model' not found - chat will 404 until: ollama pull $Model"
        Write-Warning ("Installed: " + ($names -join ", "))
        return $false
    }
    catch {
        Write-Warning ("Ollama unreachable at " + $OllamaUrl + " - " + $_.Exception.Message)
        return $false
    }
}

Write-Host "=== ALLSTART: Ollama chat model ==="
[void](Test-OllamaChatModel -Model "gemma4:latest")

# --- Tailscale serve: AllowedHosts sync (before Host start) ---
# Resolves Tailscale IP + MagicDNS and patches appsettings.json AllowedHosts
# so the Host will accept proxied requests once it starts. Soft-fail: a missing
# Tailscale CLI or sync error must NOT block local startup.
$tailscaleSynced = $false
if ($SkipTailscaleServe) {
    Write-Host "=== ALLSTART: Tailscale serve skipped (-SkipTailscaleServe) ==="
} elseif (-not (Test-Path -LiteralPath $TailscaleServe)) {
    Write-Warning "tailscale-serve-soulcore.ps1 not found at $TailscaleServe - skipping"
} else {
    Write-Host "=== ALLSTART: Tailscale AllowedHosts sync ==="
    try {
        $tsArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$TailscaleServe,"-SyncAllowedHostsOnly")
        $tsResult = Invoke-ScriptWithTimeout `
            -Label "tailscale AllowedHosts sync" `
            -ArgumentList $tsArgs `
            -WorkingDirectory $RepoRoot `
            -TimeoutSec $TailscaleTimeoutSec
        if ($tsResult.TimedOut) {
            Write-Warning "Tailscale AllowedHosts sync timed out - continuing"
        } elseif ($tsResult.ExitCode -eq 0) {
            $tailscaleSynced = $true
        } else {
            Write-Warning "tailscale-serve-soulcore.ps1 -SyncAllowedHostsOnly exited $($tsResult.ExitCode) - continuing"
        }
    } catch {
        Write-Warning "Tailscale AllowedHosts sync failed - $($_.Exception.Message)"
        Write-Warning "Continuing without Tailscale serve. Host stays loopback-only."
    }
}

Write-Host "=== ALLSTART: BrowserCaptureBridge :17891 ==="
$browserBridgeOk = $false
if ($SkipBrowserBridge) {
    Write-Host "Browser bridge skipped (-SkipBrowserBridge)."
} elseif (-not (Test-Path -LiteralPath $StartBrowserBridge)) {
    Write-Warning "Missing $StartBrowserBridge - browser_capture_tab will fail until bridge is started"
} else {
    try {
        $bbArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $StartBrowserBridge)
        $bbResult = Invoke-ScriptWithTimeout `
            -Label "start-browser-bridge" `
            -ArgumentList $bbArgs `
            -WorkingDirectory $RepoRoot `
            -TimeoutSec $BrowserBridgeTimeoutSec
        if ($bbResult.TimedOut) {
            Write-Warning "start-browser-bridge timed out after ${BrowserBridgeTimeoutSec}s - continuing"
        } elseif ($bbResult.ExitCode -ne 0) {
            Write-Warning "start-browser-bridge exited $($bbResult.ExitCode) - continuing"
        } else {
            try {
                $bh = Invoke-RestMethod -Uri "http://127.0.0.1:17891/health" -TimeoutSec 3 -ErrorAction Stop
                if ($bh.ok -eq $true) {
                    Write-Host "BrowserCaptureBridge OK: http://127.0.0.1:17891/health"
                    $browserBridgeOk = $true
                }
            } catch {
                Write-Warning "Browser bridge start reported OK but /health not ready - $($_.Exception.Message)"
            }
        }
    } catch {
        Write-Warning "BrowserCaptureBridge start failed - $($_.Exception.Message)"
    }
    $extDir = Join-Path $RepoRoot "BrowserCaptureExtension"
    Write-Host "Chrome extension (Load unpacked): $extDir"
}
if (-not $browserBridgeOk -and -not $SkipBrowserBridge) {
    Write-Warning "BrowserCaptureBridge not confirmed on :17891 (soft-fail; Host continues)."
    Write-Warning "Load unpacked extension from BrowserCaptureExtension after bridge is up."
}

Write-Host "CUA gate: AllowComputerControl stays off until ChatDesktop Services or Tools and Access enable it."

Write-Host "=== ALLSTART: locate local Victoria Host ==="
$chosenPort = $Port
$existing = Get-HealthObject -LocalPort $Port

if (Test-LocalVictoriaHealth -Health $existing) {
    if ($RestartHost) {
        Write-Host "RestartHost: replacing local Victoria on :$Port"
        Start-LocalSoulCore -LocalPort $Port
        Wait-LocalVictoria -LocalPort $Port -TimeoutSec $HealthTimeoutSec | Out-Null
    } else {
        Write-Host "Already running local Victoria on :$Port"
        Write-Host "  memory: $($existing.memory.path)"
        Write-Host "  Tip: after .env / guestcontrol changes use: .\ALLSTART.ps1 -RestartHost"
    }
} elseif ($null -ne $existing) {
    Write-Warning "Port $Port answers /health but is NOT this machine's Victoria."
    Write-Warning "  foreign memory.path = $($existing.memory.path)"
    Write-Warning "  (Often Cursor cloud port-forward / remote agent Host.)"
    Write-Warning "Starting local SoulCore on alternate port $AlternatePort instead."
    $chosenPort = $AlternatePort
    $alt = Get-HealthObject -LocalPort $AlternatePort
    if (-not (Test-LocalVictoriaHealth -Health $alt)) {
        Write-Host "=== ALLSTART: SoulCore.Host :$chosenPort ==="
        Start-LocalSoulCore -LocalPort $chosenPort
        Wait-LocalVictoria -LocalPort $chosenPort -TimeoutSec $HealthTimeoutSec | Out-Null
    } else {
        Write-Host "Local Victoria already on :$AlternatePort"
    }
} else {
    Write-Host "=== ALLSTART: SoulCore.Host :$chosenPort ==="
    Start-LocalSoulCore -LocalPort $chosenPort
    # start-soulcore exits early if *anything* listens - re-validate identity
    $after = Get-HealthObject -LocalPort $chosenPort
    if (-not (Test-LocalVictoriaHealth -Health $after)) {
        if ($null -ne $after) {
            Write-Warning "Port $chosenPort still foreign after start-soulcore (race/stolen port)."
            Write-Warning "  foreign memory.path = $($after.memory.path)"
            $chosenPort = $AlternatePort
            Write-Warning "Falling back to :$chosenPort"
            Start-LocalSoulCore -LocalPort $chosenPort
        }
        Wait-LocalVictoria -LocalPort $chosenPort -TimeoutSec $HealthTimeoutSec | Out-Null
    } else {
        Write-Host "Local Victoria Host healthy on :$chosenPort"
        Write-Host "  memory: $($after.memory.path)"
    }
}

# Point ChatDesktop at the Host we validated (not a cloud tunnel on 7700).
$env:HOUSE_SOULCORE_HOST = "127.0.0.1"
$env:HOUSE_SOULCORE_PORT = "$chosenPort"
Write-Host "GUI target: $($env:HOUSE_SOULCORE_HOST):$($env:HOUSE_SOULCORE_PORT)"

# OPS-198: ensure Playwright Chromium for BrowserBackend=playwright (soft-fail).
$InstallPlaywright = Join-Path $RepoRoot "SoulCore\scripts\install-playwright.ps1"
if (Test-Path -LiteralPath $InstallPlaywright) {
    Write-Host "=== ALLSTART: Playwright Chromium (OPS-198, soft-fail) ==="
    try {
        $pwArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$InstallPlaywright)
        $pwResult = Invoke-ScriptWithTimeout `
            -Label "install-playwright" `
            -ArgumentList $pwArgs `
            -WorkingDirectory $RepoRoot `
            -TimeoutSec 180
        if ($pwResult.TimedOut) {
            Write-Warning "install-playwright timed out — continuing (browser_* may fail until Chromium is installed)"
        } elseif ($pwResult.ExitCode -ne 0) {
            Write-Warning "install-playwright exited $($pwResult.ExitCode) — continuing (set BrowserBackend=native to use Chrome extension)"
        } else {
            Write-Host "Playwright Chromium OK (Victoria profile under LocalAppData\SoulCore\victoria-browser)"
        }
    } catch {
        Write-Warning "install-playwright failed: $($_.Exception.Message) — continuing"
    }
} else {
    Write-Warning "install-playwright.ps1 missing — skip Playwright bootstrap"
}

# --- Tailscale serve: enable proxies now that Host is healthy ---
# Applies TCP :7700 + HTTPS :8443 (tailnet-only). Soft-fail: local desktop
# still works without it. Skipped entirely if -SkipTailscaleServe or sync failed.
if ($SkipTailscaleServe) {
    Write-Host "=== ALLSTART: Tailscale serve enable skipped (-SkipTailscaleServe) ==="
} elseif (-not $tailscaleSynced) {
    Write-Warning "Skipping Tailscale serve enable (AllowedHosts sync did not succeed)."
} elseif (-not (Test-Path -LiteralPath $TailscaleServe)) {
    Write-Warning "tailscale-serve-soulcore.ps1 not found - cannot enable serve"
} else {
    Write-Host "=== ALLSTART: Tailscale serve enable (TCP 7700 + HTTPS 8443) ==="
    try {
        $tsArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$TailscaleServe,"-SkipAllowedHosts")
        $tsResult = Invoke-ScriptWithTimeout `
            -Label "tailscale serve enable" `
            -ArgumentList $tsArgs `
            -WorkingDirectory $RepoRoot `
            -TimeoutSec $TailscaleTimeoutSec
        if ($tsResult.TimedOut) {
            Write-Warning "Tailscale serve enable timed out - continuing (local desktop still works)"
        } elseif ($tsResult.ExitCode -ne 0) {
            Write-Warning "tailscale-serve-soulcore.ps1 enable exited $($tsResult.ExitCode) - continuing"
        }
    } catch {
        Write-Warning "Tailscale serve enable failed - $($_.Exception.Message)"
        Write-Warning "Local desktop still works; phone companion will not reach Host over Tailscale."
    }
}

Write-Host "=== ALLSTART: House.Voice (STT + Chatterbox TTS) ==="
$StartStt = Join-Path $RepoRoot "House\House.Voice\start-stt.ps1"
$StartTts = Join-Path $RepoRoot "House\House.Voice\start-tts.ps1"
if ($SkipVoice) {
    Write-Host "Voice skipped (-SkipVoice)."
} else {
    if (Test-Path -LiteralPath $StartStt) {
        try {
            $sttArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$StartStt)
            $sttResult = Invoke-ScriptWithTimeout `
                -Label "start-stt" `
                -ArgumentList $sttArgs `
                -WorkingDirectory $RepoRoot `
                -TimeoutSec $VoiceTimeoutSec
            if ($sttResult.TimedOut) {
                Write-Warning "STT start timed out after ${VoiceTimeoutSec}s - continuing"
            } elseif ($sttResult.ExitCode -ne 0) {
                Write-Warning "STT start exited $($sttResult.ExitCode) - continuing"
            }
        } catch { Write-Warning "STT start: $_" }
    } else {
        Write-Warning "Missing $StartStt"
    }
    if (Test-Path -LiteralPath $StartTts) {
        try {
            $ttsArgs = @("-NoProfile","-ExecutionPolicy","Bypass","-File",$StartTts)
            $ttsResult = Invoke-ScriptWithTimeout `
                -Label "start-tts" `
                -ArgumentList $ttsArgs `
                -WorkingDirectory $RepoRoot `
                -TimeoutSec $VoiceTimeoutSec
            if ($ttsResult.TimedOut) {
                Write-Warning "TTS start timed out after ${VoiceTimeoutSec}s - continuing"
            } elseif ($ttsResult.ExitCode -ne 0) {
                Write-Warning "TTS start exited $($ttsResult.ExitCode) - continuing"
            }
        } catch { Write-Warning "TTS start: $_" }
    } else {
        Write-Warning "Missing $StartTts"
    }
}

Write-Host "=== ALLSTART: House.ChatDesktop (Victoria Presence) ==="
& $StartGui -Configuration $Configuration
