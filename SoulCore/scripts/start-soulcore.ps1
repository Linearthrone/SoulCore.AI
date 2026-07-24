#Requires -Version 5.1
<#
.SYNOPSIS
  Build (if needed) and start SoulCore.Host on loopback :7700.
.NOTES
  SEC-004: V1 binds 127.0.0.1 only. Does not use 0.0.0.0.
#>
[CmdletBinding()]
param(
    [int]$Port = 7700,
    [switch]$ForceRebuild
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$SoulCoreRoot = Split-Path -Parent $ScriptsDir
$HostProject = Join-Path $SoulCoreRoot "SoulCore.Host\SoulCore.Host.csproj"
$Sln = Join-Path $SoulCoreRoot "SoulCore.sln"
$PidFile = Join-Path $ScriptsDir ".soulcore-host.pid"
$LogFile = Join-Path $ScriptsDir ".soulcore-host.log"
$BindAddress = "127.0.0.1"
$HealthUrl = "http://${BindAddress}:${Port}/health"

if (-not (Test-Path -LiteralPath $HostProject)) {
    throw "Host project not found: $HostProject"
}

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

if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
    Write-Host "SoulCore already listening on ${BindAddress}:${Port}"
    Write-Host "Health: $HealthUrl"
    exit 0
}

$dll = Join-Path $SoulCoreRoot "SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll"
$needBuild = $ForceRebuild -or -not (Test-Path -LiteralPath $dll)

if ($needBuild) {
    Write-Host "Building SoulCore..."
    & dotnet build $Sln -c Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed (exit $LASTEXITCODE)"
    }
}

if (-not (Test-Path -LiteralPath $dll)) {
    throw "Host DLL missing after build: $dll"
}

# Ensure Host config uses loopback (env overrides; Host also refuses non-loopback).
$env:Host__BindAddress = $BindAddress
$env:Host__Port = "$Port"

# Load SOULCORE_* from SoulCore/.env into process env before Start-Process
# (child inherits). Never log values. Skip comments/blank; do not overwrite non-empty env.
$EnvFile = Join-Path $SoulCoreRoot ".env"
$loadedCount = 0
if (Test-Path -LiteralPath $EnvFile) {
    foreach ($line in Get-Content -LiteralPath $EnvFile -Encoding utf8) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
        if ($trimmed.StartsWith("#")) { continue }
        $eq = $trimmed.IndexOf("=")
        if ($eq -lt 1) { continue }
        $key = $trimmed.Substring(0, $eq).Trim()
        if ($key -notlike "SOULCORE_*") { continue }
        $existing = [Environment]::GetEnvironmentVariable($key, "Process")
        if (-not [string]::IsNullOrEmpty($existing)) { continue }
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
        $loadedCount++
    }
    Write-Host "loaded $loadedCount SOULCORE_* keys from .env"
} else {
    Write-Host ".env not found at $EnvFile (skipping SOULCORE_* load)"
}

Write-Host "Starting SoulCore.Host on http://${BindAddress}:${Port} ..."
$proc = Start-Process -FilePath "dotnet" `
    -ArgumentList @($dll, "--urls", "http://${BindAddress}:${Port}") `
    -WorkingDirectory (Split-Path -Parent $dll) `
    -WindowStyle Hidden `
    -RedirectStandardOutput $LogFile `
    -RedirectStandardError "${LogFile}.err" `
    -PassThru

$proc.Id | Set-Content -LiteralPath $PidFile -Encoding ascii

$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 250
    if ($proc.HasExited) {
        $err = if (Test-Path "${LogFile}.err") { Get-Content "${LogFile}.err" -Raw } else { "" }
        throw "SoulCore.Host exited early (code $($proc.ExitCode)). stderr:`n$err"
    }
    if (Test-PortListening -LocalPort $Port -Address $BindAddress) {
        $ready = $true
        break
    }
}

if (-not $ready) {
    Write-Warning "Process started (PID $($proc.Id)) but port not confirmed listening yet."
}

Write-Host "PID: $($proc.Id)"
Write-Host "PID file: $PidFile"
Write-Host "Log: $LogFile"
Write-Host "Health: $HealthUrl"
