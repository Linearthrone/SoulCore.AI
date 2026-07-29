# TASK-118: launch UnrealEditor + PIE visual walk gate
$ErrorActionPreference = "Stop"
$Editor = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe"
$Project = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$Script = "C:/Users/kurtw/Soul_Core/tools/ue_nav/task118_pie_visual_walk.py"
$Evidence = "C:\Users\kurtw\Soul_Core\tmpcode\qa118-evidence"
New-Item -ItemType Directory -Force -Path $Evidence | Out-Null
Remove-Item (Join-Path $Evidence "task118_summary.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $Evidence "task118_pie_walk.log") -ErrorAction SilentlyContinue

Get-Process UnrealEditor, UnrealEditor-Cmd, CrashReportClientEditor -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# MUST be one argv token — spaces in "py path" otherwise split and ExecCmds becomes only "py"
$execCmdsArg = "-ExecCmds=`"py $Script`""
$logArg = "-log=`"$((Join-Path $Evidence 'task118_editor.log') -replace '\\','/')`""
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
Start-Sleep -Seconds 2
Get-CimInstance Win32_Process -Filter "ProcessId=$($p.Id)" | Select-Object -ExpandProperty CommandLine

$deadline = (Get-Date).AddMinutes(15)
$summary = Join-Path $Evidence "task118_summary.json"
$log = Join-Path $Evidence "task118_pie_walk.log"
while ((Get-Date) -lt $deadline) {
    if (Test-Path $log) {
        Write-Host "--- log tail ---"
        Get-Content $log -Tail 6
    }
    # Also note :8888 once PIE is up (optional bridge evidence)
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $iar = $c.BeginConnect("127.0.0.1", 8888, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(200)
        if ($ok -and $c.Connected) { Write-Host "8888 OPEN" }
        $c.Close()
    } catch {}

    if (Test-Path $summary) {
        Start-Sleep -Seconds 3
        Write-Host "--- SUMMARY ---"
        Get-Content $summary
        Start-Sleep -Seconds 8
        if (-not $p.HasExited) {
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
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
if (Test-Path $log) { Get-Content $log -Tail 80 }
exit 2
