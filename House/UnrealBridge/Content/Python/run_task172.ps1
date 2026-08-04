# BED-172 / TASK-172 — Run Kayleigh player pawn setup scripts on Shadow PC (UE 5.8).
# Copy scripts from SoulCore repo to MyProject Content/Python, then execute in order.

param(
    [string]$MyProjectRoot = $env:MYPROJECT_ROOT,
    [string]$EngineCmd = $env:UE_ENGINE_CMD,
    [string]$Map = "/Game/Home"
)

$ErrorActionPreference = "Continue"

function Resolve-MyProjectRoot {
    param([string]$Root)
    $candidates = @()
    if ($Root) { $candidates += $Root }
    $candidates += @(
        "C:\HouseVictoriaUE5.8\MyProject",
        "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject",
        "$env:USERPROFILE\OneDrive\Documents\Unreal Projects\MyProject"
    )
    foreach ($c in $candidates) {
        if (-not $c) { continue }
        $uproject = Join-Path $c "MyProject.uproject"
        if (Test-Path $uproject) {
            return (Resolve-Path $c).Path
        }
    }
    return $null
}

function Resolve-EngineCmd {
    param([string]$Cmd)
    if ($Cmd -and (Test-Path $Cmd)) { return $Cmd }
    $default = "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe"
    if (Test-Path $default) { return $default }
    return $null
}

$ProjectRoot = Resolve-MyProjectRoot -Root $MyProjectRoot
$EditorCmd = Resolve-EngineCmd -Cmd $EngineCmd

if (-not $ProjectRoot) {
    Write-Host "ERROR: MyProject root not found. Set MYPROJECT_ROOT or sync project to a known path."
    exit 2
}
if (-not $EditorCmd) {
    Write-Host "ERROR: UnrealEditor-Cmd.exe not found. Set UE_ENGINE_CMD or install UE 5.8."
    exit 2
}

$UProject = Join-Path $ProjectRoot "MyProject.uproject"
$PythonDir = Join-Path $ProjectRoot "Content\Python"
# Copy from repo when this script lives in SoulCore checkout
$SoulCorePython = $PSScriptRoot
$marker = Join-Path $SoulCorePython "create_bp_kayleigh_character.py"
if (-not (Test-Path $marker)) {
    Write-Host "ERROR: create_bp_kayleigh_character.py not next to run_task172.ps1"
    exit 2
}

# If we are running from SoulCore (not yet inside MyProject Content\Python), copy into project
$runningFromProject = ($PythonDir -eq (Resolve-Path $SoulCorePython).Path)
if (-not $runningFromProject) {
    Write-Host "Syncing Python scripts from repo: $SoulCorePython -> $PythonDir"
    New-Item -ItemType Directory -Force -Path $PythonDir | Out-Null
    Copy-Item -Path (Join-Path $SoulCorePython "*.py") -Destination $PythonDir -Force
    Copy-Item -Path (Join-Path $SoulCorePython "run_task172.ps1") -Destination $PythonDir -Force
} else {
    Write-Host "Scripts already in project Content\Python: $PythonDir"
}

$scripts = @(
    "create_bp_kayleigh_character.py",
    "setup_kayleigh_gamemode.py",
    "setup_kayleigh_prox_audio.py",
    "verify_kayleigh_player.py"
)

$running = Get-Process -Name "UnrealEditor", "UnrealEditor-Cmd" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "ERROR: Unreal Editor already running (PIDs: $($running.Id -join ', ')). Close before headless Cmd run."
    exit 2
}

$overallFail = $false

foreach ($script in $scripts) {
    $scriptPath = Join-Path $PythonDir $script
    if (-not (Test-Path $scriptPath)) {
        Write-Host "ERROR: Missing script $scriptPath"
        $overallFail = $true
        continue
    }

    $logName = [System.IO.Path]::GetFileNameWithoutExtension($script)
    $cmdLog = Join-Path $ProjectRoot "Saved\Logs\${logName}_cmd.log"

    Write-Host ""
    Write-Host "========== Running $script =========="
    Write-Host "Engine: $EditorCmd"
    Write-Host "Project: $UProject"
    Write-Host "Map: $Map"

    # Use forward slashes for UE -ExecutePythonScript path
    $scriptArg = ($scriptPath -replace '\\', '/')

    $proc = Start-Process -FilePath $EditorCmd -ArgumentList @(
        "`"$UProject`"",
        $Map,
        "-ExecutePythonScript=`"$scriptArg`"",
        "-unattended",
        "-nosplash",
        "-nosound",
        "-stdout",
        "-FullStdOutLogOutput",
        "-log=`"$cmdLog`""
    ) -PassThru -NoNewWindow -Wait

    Write-Host "ExitCode=$($proc.ExitCode)"

    $resultLog = Join-Path $ProjectRoot "Saved\Logs\${logName}.log"
    if (Test-Path $resultLog) {
        Write-Host "---- $logName.log (tail) ----"
        Get-Content $resultLog -Tail 25
        $tail = (Get-Content $resultLog -Tail 15) -join "`n"
        if ($tail -notmatch "RESULT:\s*PASS") {
            Write-Host "FAIL: $script did not report RESULT: PASS"
            $overallFail = $true
        }
    }
    else {
        Write-Host "WARN: Result log missing ($resultLog); grepping cmd log"
        if (Test-Path $cmdLog) {
            Select-String -Path $cmdLog -Pattern "RESULT:|FAIL|FATAL|PASS" | Select-Object -Last 30 | ForEach-Object { $_.Line }
        }
        if ($proc.ExitCode -ne 0) {
            $overallFail = $true
        }
    }
}

Write-Host ""
if ($overallFail) {
    Write-Host "TASK-172 OVERALL: FAIL — fix logs under $ProjectRoot\Saved\Logs\"
    exit 1
}

Write-Host "TASK-172 OVERALL: PASS — open /Game/Home, PIE, confirm Kayleigh possessed (not DefaultPawn)."
Write-Host "Manual if needed: World Settings GameMode Override = GM_HouseVictoria; wire Enhanced Input V for prox talk."
exit 0
