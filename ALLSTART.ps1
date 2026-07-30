#Requires -Version 5.1
<#
.SYNOPSIS
  Start local SoulCore.Host (Victoria), wait until healthy, then launch House.ChatDesktop.
.DESCRIPTION
  Refuses to attach the GUI to a foreign :7700 occupant (e.g. Cursor cloud port-forward
  to a Linux/ubuntu Host). If 7700 is stolen, starts local Host on -AlternatePort and
  points the GUI there via HOUSE_SOULCORE_PORT.
.EXAMPLE
  .\ALLSTART.ps1
  .\ALLSTART.ps1 -SkipPreflight
  .\ALLSTART.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [int]$Port = 7700,
    [int]$AlternatePort = 7701,
    [switch]$SkipPreflight,
    [switch]$ForceRebuild,
    [int]$HealthTimeoutSec = 45
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$StartHost = Join-Path $RepoRoot "SoulCore\scripts\start-soulcore.ps1"
$StartGui = Join-Path $RepoRoot "start-desktopgui.ps1"

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

    # Cloud / Linux agent Host — not this machine's Victoria
    if ($path -match '^/home/' -or $path -match '/\.local/share/SoulCore') { return $false }
    if ($path -match '(?i)/ubuntu/') { return $false }

    return $false
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

    $hostProc = Start-Process -FilePath "powershell.exe" `
        -ArgumentList $hostArgList `
        -WorkingDirectory $RepoRoot `
        -Wait -PassThru -NoNewWindow
    if ($hostProc.ExitCode -ne 0) {
        throw "start-soulcore.ps1 failed on port $LocalPort (exit $($hostProc.ExitCode))"
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

Write-Host "=== ALLSTART: locate local Victoria Host ==="
$chosenPort = $Port
$existing = Get-HealthObject -LocalPort $Port

if (Test-LocalVictoriaHealth -Health $existing) {
    Write-Host "Already running local Victoria on :$Port"
    Write-Host "  memory: $($existing.memory.path)"
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
    # start-soulcore exits early if *anything* listens — re-validate identity
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

Write-Host "=== ALLSTART: House.ChatDesktop (Victoria Presence) ==="
& $StartGui -Configuration $Configuration
