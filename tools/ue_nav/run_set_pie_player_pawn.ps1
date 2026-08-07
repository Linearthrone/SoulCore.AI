# BED-184: live-fix PIE so Kurt possesses BP_MHC_Kayleigh (not flying DefaultPawn).
# Requires UE 5.8. Prefer: Editor already open on /Game/Home with Python enabled.
# Fallback: launches MyProject + /Game/Home and runs the py script via -ExecCmds.
$ErrorActionPreference = "Stop"

$Editor = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe"
$Project = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$RepoRoot = "C:\Users\kurtw\Soul_Core"
$ScriptWin = Join-Path $RepoRoot "tools\ue_nav\set_pie_player_pawn.py"
$ScriptPy = ($ScriptWin -replace '\\', '/')
$Evidence = Join-Path $RepoRoot "tmpcode\bed184-pie-pawn"
$LogPath = Join-Path $Evidence "set_pie_player_pawn.log"
$Remote = "http://127.0.0.1:30010"

New-Item -ItemType Directory -Force -Path $Evidence | Out-Null

if (-not (Test-Path $ScriptWin)) {
    throw "Missing $ScriptWin — pull branch cursor/bed-184-eyes-view-and-pie-avatar-169c (or main after merge)."
}
if (-not (Test-Path $Project)) {
    throw "Missing uproject: $Project"
}

function Test-RemoteControl {
    try {
        $r = Invoke-WebRequest -Uri "$Remote/remote/info" -UseBasicParsing -TimeoutSec 2
        return $r.StatusCode -ge 200
    } catch {
        return $false
    }
}

function Invoke-EditorPython([string]$pyPath) {
    # Remote Control: ExecuteConsoleCommand "py <path>"
    $body = @{
        objectPath          = "/Script/Engine.Default__KismetSystemLibrary"
        functionName        = "ExecuteConsoleCommand"
        parameters          = @{
            WorldContextObject = "/Engine/Transient.UnrealEditorEngine_0"
            Command            = "py $pyPath"
            SpecificPlayer     = $null
        }
        generateTransaction = $false
    } | ConvertTo-Json -Depth 6

    Invoke-WebRequest -Uri "$Remote/remote/object/call" -Method PUT -Body $body -ContentType "application/json" -UseBasicParsing | Out-Null
}

Write-Host "BED-184 PIE player pawn → BP_MHC_Kayleigh"
Write-Host "Script: $ScriptWin"

$editorProcs = @(Get-Process UnrealEditor -ErrorAction SilentlyContinue)
if ($editorProcs.Count -gt 0 -and (Test-RemoteControl)) {
    Write-Host "UnrealEditor already running + Remote Control :30010 — executing py live…"
    Invoke-EditorPython $ScriptPy
    Write-Host "Done. Check Output Log for [set_pie_player_pawn]. Then press Play (PIE)."
    Write-Host "If still a ghost: BP_MHC_Kayleigh may be a bare Actor — see log and reparent to Character."
    exit 0
}

Write-Host "Launching UnrealEditor /Game/Home with -ExecCmds py …"
# MUST be one argv token
$execCmdsArg = "-ExecCmds=`"py $ScriptPy`""
$logArg = "-log=`"$((Join-Path $Evidence 'editor.log') -replace '\\','/')`""

$p = Start-Process -FilePath $Editor -ArgumentList @(
    "`"$Project`"",
    "/Game/Home",
    $execCmdsArg,
    "-nosplash",
    $logArg
) -PassThru

Write-Host "PID=$($p.Id). Wait for Output Log [set_pie_player_pawn] DONE, then Play."
Write-Host "Evidence dir: $Evidence"
exit 0
