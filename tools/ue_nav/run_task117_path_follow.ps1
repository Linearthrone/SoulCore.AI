# TASK-117: launch UnrealEditor with tick-safe ExecCmds path-follow verify
$ErrorActionPreference = "Stop"
$Editor = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe"
$Project = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$Script = "C:/Users/kurtw/Soul_Core/tools/ue_nav/task117_path_follow_verify.py"
$Evidence = "C:\Users\kurtw\Soul_Core\tmpcode\qa117-evidence"
New-Item -ItemType Directory -Force -Path $Evidence | Out-Null
Remove-Item (Join-Path $Evidence "task117_summary.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Evidence "task117_path_follow.log") -ErrorAction SilentlyContinue

Get-Process UnrealEditor, UnrealEditor-Cmd -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Single ExecCmds arg with quoted py path (UE parser)
$exec = "py $Script"
$argList = @(
    "`"$Project`"",
    "/Game/Home",
    "-ExecCmds=`"$exec`"",
    "-nosplash",
    "-nosound",
    "-log"
)
$cmdLine = "& `"$Editor`" " + ($argList -join " ")
Write-Host "Launching: $cmdLine"
$p = Start-Process -FilePath $Editor -ArgumentList @(
    $Project,
    "/Game/Home",
    "-ExecCmds=$exec",
    "-nosplash",
    "-nosound",
    "-log"
) -PassThru
Write-Host "PID=$($p.Id)"

$deadline = (Get-Date).AddMinutes(12)
$summary = Join-Path $Evidence "task117_summary.json"
$log = Join-Path $Evidence "task117_path_follow.log"
while ((Get-Date) -lt $deadline) {
    if (Test-Path $log) {
        Write-Host "--- log tail ---"
        Get-Content $log -Tail 8
    }
    if (Test-Path $summary) {
        Start-Sleep -Seconds 2
        Write-Host "--- SUMMARY ---"
        Get-Content $summary
        # allow quit_editor
        Start-Sleep -Seconds 5
        exit 0
    }
    if ($p.HasExited -and -not (Test-Path $summary)) {
        Write-Host "Editor exited code=$($p.ExitCode) without summary"
        if (Test-Path $log) { Get-Content $log }
        exit 3
    }
    Start-Sleep -Seconds 8
}
Write-Host "SUMMARY_MISSING"
if (Test-Path $log) { Get-Content $log }
exit 2
