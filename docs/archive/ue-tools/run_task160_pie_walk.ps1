# TASK-160: rebuild bridge then PIE visual walk verify
$ErrorActionPreference = "Stop"
$Editor = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe"
$Project = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$Script = "C:/Users/kurtw/Soul_Core/tools/ue_nav/task160_pie_visual_walk.py"
$Evidence = "C:\Users\kurtw\Soul_Core\tmpcode\qa160-evidence"
$BuildPs1 = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\build_bridge.ps1"

New-Item -ItemType Directory -Force -Path $Evidence | Out-Null
Remove-Item (Join-Path $Evidence "task160_summary.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Evidence "task160_pie_walk.log") -ErrorAction SilentlyContinue

Get-Process UnrealEditor, UnrealEditor-Cmd, CrashReportClientEditor -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

Write-Host "=== Building HouseVictoriaBridge ==="
& $BuildPs1
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD_FAILED exit=$LASTEXITCODE"
    exit 1
}

$execCmdsArg = "-ExecCmds=`"py $Script`""
$logArg = "-log=`"$((Join-Path $Evidence 'task160_editor.log') -replace '\\','/')`""
Write-Host "Launching UnrealEditor with $execCmdsArg"
$p = Start-Process -FilePath $Editor -ArgumentList @(
    "`"$Project`"",
    "/Game/Home",
    $execCmdsArg,
    "-nosplash",
    "-nosound",
    $logArg
) -PassThru
Write-Host "PID=$($p.Id)"

$deadline = (Get-Date).AddMinutes(18)
$summary = Join-Path $Evidence "task160_summary.json"
$log = Join-Path $Evidence "task160_pie_walk.log"
while ((Get-Date) -lt $deadline) {
    if (Test-Path $log) {
        Write-Host "--- log tail ---"
        Get-Content $log -Tail 8
    }
    if (Test-Path $summary) {
        Start-Sleep -Seconds 4
        Write-Host "--- SUMMARY ---"
        Get-Content $summary
        Start-Sleep -Seconds 8
        if (-not $p.HasExited) {
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
        $j = Get-Content $summary -Raw | ConvertFrom-Json
        if ($j.overall -eq $true) { exit 0 } else { exit 4 }
    }
    if ($p.HasExited -and -not (Test-Path $summary)) {
        Write-Host "Editor exited code=$($p.ExitCode) without summary"
        if (Test-Path $log) { Get-Content $log }
        exit 3
    }
    Start-Sleep -Seconds 8
}
Write-Host "SUMMARY_MISSING"
if (Test-Path $log) { Get-Content $log -Tail 80 }
exit 2
