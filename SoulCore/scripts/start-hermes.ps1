#Requires -Version 5.1
<#
.SYNOPSIS
  Start the Hermes Agent gateway (OpenAI-compatible API) on loopback :8642.
.DESCRIPTION
  Restores/starts the LLMOD Hermes Python gateway used for MCP desktop/browser/trading
  tools (TASK-143). Does NOT flip SoulCore Hermes.Enabled (BED-144 owns that toggle).

  Preflight:
    * Resolve hermes.exe (PATH or %LOCALAPPDATA%\hermes\hermes-agent\venv)
    * Confirm config.yaml exists (hermes config path)
    * Confirm required MCP servers enabled in config (house_victoria,
      house_victoria_data, computer_use) - warn if missing
    * OPS-178: rewrite MCP command python.exe → pythonw.exe when sibling
      pythonw.exe exists (hides blank consoles; see patch-hermes-mcp-pythonw.ps1)
    * Clear PYTHONPATH/VIRTUAL_ENV so a project venv cannot break pydantic_core
    * Load %LOCALAPPDATA%\hermes\.env into the child process env
    * Probe Ollama :11434 (advisory; gateway still starts)

  Runtime artifacts (this scripts dir):
    * .hermes.pid  - outer Start-Process PID
    * .hermes.log / .hermes.log.err - redirected stdout/stderr

  Stop / restart:
    Stop:    hermes gateway stop
             (or Stop-Process -Id (Get-Content .hermes.pid); also clear :8642)
    Restart: hermes gateway stop; then re-run this script
             (or: hermes gateway restart if the Windows service is installed)

.NOTES
  Source install: %LOCALAPPDATA%\hermes\hermes-agent (CLI hermes.exe)
  Config:         %LOCALAPPDATA%\hermes\config.yaml  (hermes config path)
  MCP Python:     LLMOD\...\MCPServer\.venv (house_victoria stdio)
  Bind:           127.0.0.1:8642 only (API_SERVER_* in .env)
#>
[CmdletBinding()]
param(
    [int]$Port = 8642,
    [string]$BindAddress = "127.0.0.1",
    [string]$LlmodRoot = "C:\Users\kurtw\LLMOD\LLMOD-max-master",
    [string]$OllamaUrl = "http://127.0.0.1:11434",
    [switch]$SkipPreflight,
    [switch]$ForceRestart,
    [switch]$SkipMcpPythonwPatch
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$PidFile = Join-Path $ScriptsDir ".hermes.pid"
$LogFile = Join-Path $ScriptsDir ".hermes.log"
$ErrLogFile = Join-Path $ScriptsDir ".hermes.log.err"
$HealthUrl = "http://${BindAddress}:${Port}/health"

function Test-PortListening {
    param([int]$LocalPort, [string]$Address)
    try {
        $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.LocalAddress -eq $Address }
        return $null -ne $conns
    } catch {
        return $false
    }
}

function Get-HermesExecutable {
    $cmd = Get-Command hermes -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    foreach ($venvName in @("venv", ".venv")) {
        $fallback = Join-Path $env:LOCALAPPDATA "hermes\hermes-agent\$venvName\Scripts\hermes.exe"
        if (Test-Path -LiteralPath $fallback) { return $fallback }
    }
    return $null
}

function Stop-HermesOnPort {
    param([int]$LocalPort)
    $stopped = $false
    try {
        & hermes gateway stop 2>$null | Out-Null
    } catch { }
    Start-Sleep -Seconds 1
    $conns = Get-NetTCPConnection -LocalPort $LocalPort -State Listen -ErrorAction SilentlyContinue
    foreach ($c in @($conns)) {
        Write-Host "Stopping leftover PID $($c.OwningProcess) on :$LocalPort"
        Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue
        $stopped = $true
    }
    if ($stopped) { Start-Sleep -Seconds 1 }
}

function Invoke-OllamaPreflight {
    param([string]$BaseUrl)
    $tagsUrl = ($BaseUrl.TrimEnd("/")) + "/api/tags"
    Write-Host "Preflight: probing Ollama at $tagsUrl"
    try {
        $tags = Invoke-RestMethod -Uri $tagsUrl -Method Get -TimeoutSec 5 -ErrorAction Stop
        $count = 0
        if ($null -ne $tags.models) { $count = @($tags.models).Count }
        Write-Host "Preflight OK: Ollama reachable ($count models)."
    } catch {
        Write-Warning "Ollama unreachable at $BaseUrl - $($_.Exception.Message)"
        Write-Warning "Hermes gateway will still start, but /v1/chat/completions will fail until Ollama is up."
    }
}

$hermesExe = Get-HermesExecutable
if (-not $hermesExe) {
    throw "hermes.exe not found. Restore via LLMOD Tools\setup-hermes-integration.ps1 (quarry: $LlmodRoot)"
}
Write-Host "Hermes exe: $hermesExe"

$configPath = (& $hermesExe config path 2>$null | Out-String).Trim()
if (-not $configPath -or -not (Test-Path -LiteralPath $configPath)) {
    throw "Hermes config not found (hermes config path). Expected under %LOCALAPPDATA%\hermes\config.yaml"
}
$hermesDir = Split-Path -Parent $configPath
$envFile = Join-Path $hermesDir ".env"
Write-Host "Config: $configPath"
Write-Host ".env:   $envFile (exists=$(Test-Path -LiteralPath $envFile))"

# OPS-178: hide blank MCP python consoles (runs even with -SkipPreflight unless opted out).
$mcpPythonwRewrites = 0
if (-not $SkipMcpPythonwPatch) {
    $patchScript = Join-Path $ScriptsDir "patch-hermes-mcp-pythonw.ps1"
    if (Test-Path -LiteralPath $patchScript) {
        try {
            # Patch script Write-Hosts status; pipeline output is rewrite count.
            $patchOut = & $patchScript -ConfigPath $configPath
            foreach ($item in @($patchOut)) {
                if ($null -eq $item) { continue }
                if ($item -is [int] -or ($item -is [long])) {
                    $mcpPythonwRewrites = [int]$item
                } elseif (("$item") -match '^\d+$') {
                    $mcpPythonwRewrites = [int]"$item"
                }
            }
            if ($mcpPythonwRewrites -gt 0) {
                Write-Host "OPS-178: MCP config changed ($mcpPythonwRewrites) - will restart gateway so pythonw children replace console python."
                $ForceRestart = $true
            }
        } catch {
            Write-Warning "OPS-178: MCP pythonw patch failed - $($_.Exception.Message)"
            Write-Warning "Blank python.exe consoles may still appear. Re-run: $patchScript"
        }
    } else {
        Write-Warning "OPS-178: missing $patchScript - cannot rewrite MCP python → pythonw"
    }
} else {
    Write-Host "OPS-178: MCP pythonw patch skipped (-SkipMcpPythonwPatch)."
}

if (-not $SkipPreflight) {
    $cfgText = Get-Content -LiteralPath $configPath -Raw -ErrorAction SilentlyContinue
    foreach ($name in @("house_victoria", "house_victoria_data", "computer_use")) {
        $pattern = '(?m)^\s*' + [regex]::Escape($name) + '\s*:'
        if ($cfgText -match $pattern) {
            Write-Host "Preflight OK: mcp_servers.$name present in config"
        } else {
            Write-Warning "mcp_servers.$name missing from $configPath - run LLMOD Tools\setup-hermes-integration.ps1"
        }
    }
    $hvPyw = Join-Path $LlmodRoot "MCPServer\.venv\Scripts\pythonw.exe"
    $hvPy = Join-Path $LlmodRoot "MCPServer\.venv\Scripts\python.exe"
    if (Test-Path -LiteralPath $hvPyw) {
        Write-Host "Preflight OK: house_victoria pythonw at $hvPyw (no console window)"
    } elseif (Test-Path -LiteralPath $hvPy) {
        Write-Warning "house_victoria pythonw.exe missing; console python present: $hvPy"
        Write-Warning "MCP stdio children may show blank python.exe windows (OPS-178)."
    } else {
        Write-Warning "house_victoria venv missing: $hvPy"
    }
    Invoke-OllamaPreflight -BaseUrl $OllamaUrl
} else {
    Write-Host "Preflight skipped (-SkipPreflight)."
}

if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
    try {
        $h = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 3
        if ($h.StatusCode -eq 200 -and -not $ForceRestart) {
            Write-Host "Hermes already listening on ${BindAddress}:${Port}"
            Write-Host "Health: $($h.Content)"
            exit 0
        }
    } catch {
        Write-Warning "Port $Port listening but /health failed - forcing restart"
        $ForceRestart = $true
    }
}

if ($ForceRestart -or (Test-PortListening -LocalPort $Port -Address $BindAddress)) {
    Write-Host "ForceRestart: stopping existing gateway..."
    Stop-HermesOnPort -LocalPort $Port
}

# Sanitize Python env - project venvs break Hermes (pydantic_core).
foreach ($key in @("PYTHONPATH", "VIRTUAL_ENV", "VIRTUAL_ENV_PROMPT", "CONDA_PREFIX", "CONDA_DEFAULT_ENV")) {
    Remove-Item -Path "Env:$key" -ErrorAction SilentlyContinue
}

# Load Hermes .env into this process (child inherits). Never print secret values.
$loaded = 0
if (Test-Path -LiteralPath $envFile) {
    foreach ($line in Get-Content -LiteralPath $envFile -Encoding utf8) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
        if ($trimmed.StartsWith("#")) { continue }
        $eq = $trimmed.IndexOf("=")
        if ($eq -lt 1) { continue }
        $key = $trimmed.Substring(0, $eq).Trim()
        $value = $trimmed.Substring($eq + 1).Trim()
        if (
            ($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))
        ) {
            if ($value.Length -ge 2) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }
        Set-Item -Path "Env:$key" -Value $value
        $loaded++
    }
    Write-Host "loaded $loaded keys from Hermes .env"
} else {
    Write-Warning ".env missing at $envFile - API_SERVER_ENABLED may be off"
}

$workDir = $LlmodRoot
if (-not (Test-Path -LiteralPath $workDir)) {
    $workDir = $hermesDir
}

Write-Host "Starting hermes gateway on http://${BindAddress}:${Port} ..."
if (Test-Path -LiteralPath $LogFile) { Clear-Content -LiteralPath $LogFile -ErrorAction SilentlyContinue }
if (Test-Path -LiteralPath $ErrLogFile) { Clear-Content -LiteralPath $ErrLogFile -ErrorAction SilentlyContinue }

$proc = Start-Process -FilePath $hermesExe `
    -ArgumentList @("gateway") `
    -WorkingDirectory $workDir `
    -WindowStyle Hidden `
    -RedirectStandardOutput $LogFile `
    -RedirectStandardError $ErrLogFile `
    -PassThru

$proc.Id | Set-Content -LiteralPath $PidFile -Encoding ascii

$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    if ($proc.HasExited) {
        $err = if (Test-Path -LiteralPath $ErrLogFile) { Get-Content -LiteralPath $ErrLogFile -Raw } else { "" }
        throw "hermes gateway exited early (code $($proc.ExitCode)). stderr:`n$err"
    }
    try {
        $h = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($h.StatusCode -eq 200) {
            $ready = $true
            Write-Host "Health: $($h.Content)"
            break
        }
    } catch { }
}

# Hermes may daemonize - outer PID can exit while a child holds :8642.
$listenPid = $null
$listen = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalAddress -eq $BindAddress } | Select-Object -First 1
if ($listen) { $listenPid = $listen.OwningProcess }

if (-not $ready) {
    Write-Warning "Process started (outer PID $($proc.Id)) but /health not confirmed yet. Check $ErrLogFile"
} else {
    Write-Host "Hermes gateway ready."
}

Write-Host "Outer PID: $($proc.Id)"
if ($listenPid) { Write-Host "Listen PID: $listenPid" }
Write-Host "PID file: $PidFile"
Write-Host "Log: $LogFile"
Write-Host "Health: $HealthUrl"
Write-Host "Stop:    hermes gateway stop   (or Stop-Process the listen PID on :$Port)"
Write-Host "Restart: .\start-hermes.ps1 -ForceRestart"
