# REX-01 / TASK-191 — live-fix PIE so Kurt possesses BP_KayleighCharacter (never Victoria).
# Requires UE 5.8. Prefer: Editor already open on /Game/Home with Remote Control :30010.
$ErrorActionPreference = "Stop"

$Editor = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor.exe"
$Project = "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\MyProject.uproject"
$RepoRoot = "C:\Users\kurtw\Soul_Core"
$ScriptWin = Join-Path $RepoRoot "tools\ue_nav\kayleigh_player\rex_pie_possess_kayleigh.py"
$ScriptPy = ($ScriptWin -replace '\\', '/')
$Evidence = Join-Path $RepoRoot "tmpcode\rex191-kayleigh-pie"
$Remote = "http://127.0.0.1:30010"

New-Item -ItemType Directory -Force -Path $Evidence | Out-Null

if (-not (Test-Path $ScriptWin)) {
    throw "Missing $ScriptWin — pull branch cursor/rex01-kayleigh-pie-possess-169c (or main after merge)."
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

Write-Host "REX-01 PIE possess → BP_KayleighCharacter (FORBIDDEN: Victoria / flying DefaultPawn)"
Write-Host "Script: $ScriptWin"

$editorProcs = @(Get-Process UnrealEditor -ErrorAction SilentlyContinue)
if ($editorProcs.Count -gt 0 -and (Test-RemoteControl)) {
    Write-Host "UnrealEditor + Remote Control :30010 — executing REX pipeline live…"
    Invoke-EditorPython $ScriptPy
    Write-Host "Done. Check Output Log for [rex_pie_possess_kayleigh] PASS/FAIL."
    Write-Host "Evidence: $Evidence\rex_pie_possess_kayleigh.log"
    Write-Host "Then PIE: you must be Kayleigh. If Victoria or ghost → FAIL (do not accept)."
    exit 0
}

Write-Host "Launching UnrealEditor /Game/Home with -ExecCmds py …"
$execCmdsArg = "-ExecCmds=`"py $ScriptPy`""
$logArg = "-log=`"$((Join-Path $Evidence 'editor.log') -replace '\\','/')`""

$p = Start-Process -FilePath $Editor -ArgumentList @(
    "`"$Project`"",
    "/Game/Home",
    $execCmdsArg,
    "-nosplash",
    $logArg
) -PassThru

Write-Host "PID=$($p.Id). Wait for [rex_pie_possess_kayleigh] PASS, then Play."
Write-Host "Evidence dir: $Evidence"
exit 0
