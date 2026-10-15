#Requires -Version 5.1
<#
.SYNOPSIS
  Unstick victoria-sandbox after a hung save-state / session lock.
.DESCRIPTION
  Host COM (Presence clicks/wheel) and leftover VirtualBoxVM processes can leave
  the VM "saved" or "aborted" with a lock so GUI start and poweroff both refuse.
  This script: stop local Host, kill VM front-ends, emergencystop/poweroff,
  discard saved state if needed, then start GUI.
.NOTES
  ASCII-only for Windows PowerShell 5.1. Local desk only.
#>
[CmdletBinding()]
param(
    [string]$VmName = "victoria-sandbox",
    [switch]$SkipStart
)

$ErrorActionPreference = "Continue"
$VBox = $null
foreach ($c in @(
        "${env:ProgramFiles}\Oracle\VirtualBox\VBoxManage.exe",
        "${env:ProgramFiles(x86)}\Oracle\VirtualBox\VBoxManage.exe"
    )) {
    if (Test-Path -LiteralPath $c) { $VBox = $c; break }
}
if (-not $VBox) {
    $cmd = Get-Command VBoxManage -ErrorAction SilentlyContinue
    if ($cmd) { $VBox = $cmd.Source }
}
if (-not $VBox) { throw "VBoxManage not found" }

function Invoke-VBox {
    param([string[]]$Args)
    Write-Host ("> VBoxManage " + ($Args -join " "))
    & $VBox @Args 2>&1 | ForEach-Object { Write-Host $_ }
    return $LASTEXITCODE
}

Write-Host "=== force-unlock $VmName ==="

# 1) Host COM shared-locks block poweroff/start - kill local Host herd first.
Write-Host "Stopping local SoulCore.Host processes..."
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='SoulCore.Host.exe'" -ErrorAction SilentlyContinue |
    Where-Object {
        $cl = [string]$_.CommandLine
        $cl -match 'SoulCore\.Host(\.dll|\.exe)?' -and $cl -notmatch '(?i)[\\/]Cursor[\\/]|cursor-agent'
    } |
    ForEach-Object {
        Write-Host "  Stop Host PID $($_.ProcessId)"
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

# 2) Kill VM UI / headless for this name (not the whole VBoxSVC unless needed).
Write-Host "Stopping VirtualBoxVM / VBoxHeadless front-ends..."
Get-CimInstance Win32_Process -Filter "Name='VirtualBoxVM.exe' OR Name='VBoxHeadless.exe'" -ErrorAction SilentlyContinue |
    Where-Object {
        $cl = [string]$_.CommandLine
        [string]::IsNullOrWhiteSpace($cl) -or $cl -match [regex]::Escape($VmName)
    } |
    ForEach-Object {
        Write-Host "  Stop $($_.Name) PID $($_.ProcessId)"
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

Start-Sleep -Seconds 2

Write-Host "Current showvminfo (machinereadable excerpt):"
& $VBox showvminfo $VmName --machinereadable 2>&1 |
    Select-String -Pattern '^(name|VMState|SessionName|SessionType|SessionState)=' |
    ForEach-Object { Write-Host "  $($_.Line)" }

# 3) Break a stuck session / powered-on-without-UI.
[void](Invoke-VBox @("startvm", $VmName, "--type", "emergencystop"))
Start-Sleep -Milliseconds 800
[void](Invoke-VBox @("controlvm", $VmName, "poweroff"))
Start-Sleep -Milliseconds 800

# 4) If still "saved", discard the save so startvm can run (loses RAM snapshot).
$info = & $VBox showvminfo $VmName --machinereadable 2>&1 | Out-String
if ($info -match 'VMState="saved"' -or $info -match 'VMState="aborted"') {
    Write-Host "Discarding saved/aborted state so the VM can start clean..."
    [void](Invoke-VBox @("discardstate", $VmName))
    Start-Sleep -Milliseconds 500
}

# 5) Clear stale lock files under the VM folder (best-effort).
try {
    $cfg = (& $VBox showvminfo $VmName --machinereadable 2>&1 | Select-String -Pattern '^CfgFile=' | Select-Object -First 1)
    if ($cfg) {
        $cfgPath = ($cfg.Line -replace '^CfgFile=', '').Trim('"')
        $vmDir = Split-Path -Parent $cfgPath
        if (Test-Path -LiteralPath $vmDir) {
            Get-ChildItem -LiteralPath $vmDir -Filter "*.lock" -Force -ErrorAction SilentlyContinue |
                ForEach-Object {
                    Write-Host "  Removing lock $($_.FullName)"
                    Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                }
            Get-ChildItem -LiteralPath $vmDir -Filter "*.vbox-tmp" -Force -ErrorAction SilentlyContinue |
                ForEach-Object {
                    Write-Host "  Removing $($_.Name)"
                    Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                }
        }
    }
} catch {
    Write-Warning "Lock-file sweep skipped: $($_.Exception.Message)"
}

if ($SkipStart) {
    Write-Host "SkipStart set - not launching GUI. Run: VBoxManage startvm $VmName --type gui"
    exit 0
}

Write-Host "Starting GUI..."
$code = Invoke-VBox @("startvm", $VmName, "--type", "gui")
if ($code -ne 0) {
    Write-Warning "startvm failed (exit $code). If Session is still locked, reboot Windows or:"
    Write-Warning "  Stop-Process -Name VBoxSVC -Force; then re-run this script."
    exit $code
}

# Give the top-level victoria-sandbox window a moment to appear before Host looks it up.
Start-Sleep -Seconds 2

# We killed Host above so COM locks would release. Bring one Host back so Presence
# /browser/embed can SetParent the new GUI into Her screen (otherwise it stays floating).
$startHost = Join-Path $PSScriptRoot "..\..\SoulCore\scripts\start-soulcore.ps1"
$startHost = [System.IO.Path]::GetFullPath($startHost)
if (Test-Path -LiteralPath $startHost) {
    Write-Host "Restarting SoulCore.Host so Her screen can re-embed..."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $startHost -RestartHost 2>&1 |
        ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Host restart failed (exit $LASTEXITCODE). Run ALLSTART or reopen Presence with Auto-start."
    }
} else {
    Write-Warning "start-soulcore.ps1 not found at $startHost - start Host manually, then open Her screen."
}

Write-Host "OK - $VmName GUI up + Host restarted. Focus Presence on Her screen (Embed VirtualBox on)."
exit 0
