# TASK-116: UnrealEditor + -ExecCmds py (no ExecutePythonScript — that always quits).
$ErrorActionPreference = "Continue"
$Engine = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe"
$UProject = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$Script = "C:/Users/kurtw/Soul_Core/tools/ue_nav/tick_bake_home_navmesh.py"
$LogOut = "C:\Users\kurtw\Soul_Core\tools\ue_nav\task116_editor.log"
$ScriptLog = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\Saved\Logs\task116_navmesh_tick.log"
$TimeoutSec = 900

Copy-Item "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\Content\Python\tick_bake_home_navmesh.py" `
    "C:\Users\kurtw\Soul_Core\tools\ue_nav\tick_bake_home_navmesh.py" -Force

# Bump wait budgets for cold start
$p = "C:\Users\kurtw\Soul_Core\tools\ue_nav\tick_bake_home_navmesh.py"
$c = Get-Content $p -Raw
$c = $c -replace 'MAX_WAIT_TICKS = 900','MAX_WAIT_TICKS = 1800'
$c = $c -replace 'MAX_BUILD_WAIT_TICKS = 1200','MAX_BUILD_WAIT_TICKS = 1800'
Set-Content $p -Value $c -Encoding UTF8

$running = Get-Process -Name "UnrealEditor","UnrealEditor-Cmd" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Killing leftover UE: $($running.Id -join ',')"
    $running | Stop-Process -Force
    Start-Sleep -Seconds 5
}

if (Test-Path $ScriptLog) { Remove-Item $ScriptLog -Force }

Write-Host "Launching UnrealEditor with ExecCmds py..."
Get-CimInstance Win32_OperatingSystem | ForEach-Object {
    'FreeGB={0:N2} TotalGB={1:N2}' -f ($_.FreePhysicalMemory/1MB), ($_.TotalVisibleMemorySize/1MB)
}

# No spaces in script path — safe inside ExecCmds quotes
$exec = "py $Script"
$argList = @(
    "`"$UProject`"",
    "/Game/Home",
    "-ExecCmds=`"$exec`"",
    "-nosplash",
    "-nosound",
    "-log=`"$LogOut`""
)

Write-Host "ExecCmds=$exec"
$proc = Start-Process -FilePath $Engine -ArgumentList $argList -PassThru
Write-Host "Started PID=$($proc.Id)"

$deadline = (Get-Date).AddSeconds($TimeoutSec)
$sawResult = $false
while (-not $proc.HasExited) {
    if ((Get-Date) -gt $deadline) {
        Write-Host "TIMEOUT - killing"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Get-Process -Name "UnrealEditor","UnrealEditor-Cmd" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        break
    }
    if ((Test-Path $ScriptLog) -and (-not $sawResult)) {
        $content = Get-Content $ScriptLog -Raw -ErrorAction SilentlyContinue
        if ($content -match "RESULT:\s*(PASS|FAIL)") {
            $sawResult = $true
            Write-Host "RESULT seen - waiting for QUIT_EDITOR..."
            $quitDeadline = (Get-Date).AddSeconds(120)
            while (-not $proc.HasExited -and (Get-Date) -lt $quitDeadline) {
                Start-Sleep -Seconds 3
            }
            if (-not $proc.HasExited) {
                Write-Host "Force kill after RESULT"
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            }
            break
        }
        # Progress heartbeat
        if ($content -match "tick=(\d+)") {
            # optional
        }
    }
    Start-Sleep -Seconds 5
}

if (-not $proc.HasExited) { $null = $proc.WaitForExit(30000) }
Write-Host "HasExited=$($proc.HasExited) ExitCode=$($proc.ExitCode)"

if (Test-Path $ScriptLog) {
    Write-Host "==== tick log ===="
    Get-Content $ScriptLog
    $joined = (Get-Content $ScriptLog -Tail 25) -join "`n"
    if ($joined -match "RESULT:\s*PASS") { exit 0 }
    if ($joined -match "RESULT:\s*FAIL") { exit 1 }
} else {
    Write-Host "No script log yet. Grep editor log:"
    if (Test-Path $LogOut) {
        Select-String -Path $LogOut -Pattern "Cmd: py|TASK-116|LogNavigation|0x20|tick=|RESULT|ExecCmds|Python" | Select-Object -Last 100 | ForEach-Object { $_.Line }
    }
}
exit 1
