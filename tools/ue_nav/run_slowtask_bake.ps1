# TASK-116: Cmd + ScopedSlowTask pump (no-space script path)
$ErrorActionPreference = "Continue"
$Engine = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
$UProject = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$Script = "C:/Users/kurtw/Soul_Core/tools/ue_nav/slowtask_bake_home_navmesh.py"
$LogOut = "C:\Users\kurtw\Soul_Core\tools\ue_nav\task116_cmd.log"
$ScriptLog = "C:\Users\kurtw\Soul_Core\tools\ue_nav\task116_navmesh_result.log"

$running = Get-Process -Name "UnrealEditor","UnrealEditor-Cmd" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "ERROR: Unreal already running: $($running.Id -join ',')"
    exit 2
}

if (Test-Path $ScriptLog) { Remove-Item $ScriptLog -Force }

Write-Host "Launching UnrealEditor-Cmd + ScopedSlowTask bake..."
Write-Host "Script: $Script"

$proc = Start-Process -FilePath $Engine -ArgumentList @(
    "`"$UProject`"",
    "/Game/Home",
    "-ExecutePythonScript=`"$Script`"",
    "-unattended",
    "-nosplash",
    "-nosound",
    "-stdout",
    "-FullStdOutLogOutput",
    "-log=`"$LogOut`""
) -PassThru -NoNewWindow -Wait

Write-Host "ExitCode=$($proc.ExitCode)"

if (Test-Path $ScriptLog) {
    Write-Host "==== result log ===="
    Get-Content $ScriptLog
    $joined = (Get-Content $ScriptLog -Tail 15) -join "`n"
    if ($joined -match "RESULT:\s*PASS") { exit 0 }
    if ($joined -match "RESULT:\s*FAIL") { exit 1 }
} else {
    Write-Host "Result log missing - grepping cmd log"
    if (Test-Path $LogOut) {
        Select-String -Path $LogOut -Pattern "RESULT|LogNavigation|0x20|locked|TASK-116|FATAL|slowtask" | Select-Object -Last 80 | ForEach-Object { $_.Line }
    }
}
exit 1
