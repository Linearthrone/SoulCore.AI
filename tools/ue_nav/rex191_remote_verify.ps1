# REX-191 remote PIE verify — run AFTER Kurt restarts the shadow editor on /Game/Home with Change 20 synced.
# Confirms HouseGameMode class exists, starts PIE, and reads back the possessed pawn class.
$ErrorActionPreference = 'Continue'
$Remote = 'http://house-victoria:30010'

Write-Host "=== REX-191 remote PIE verify (post shadow sync+restart) ==="

# 1. Is the editor up?
Write-Host "--- editor up? ---"
try {
    $r = Invoke-WebRequest -Uri "$Remote/remote/info" -UseBasicParsing -TimeoutSec 8
    Write-Host "REMOTE-INFO-OK status=$($r.StatusCode)"
} catch { Write-Host "REMOTE-INFO-FAIL: $($_.Exception.Message) — editor not up yet"; exit 1 }

# 2. Does HouseGameMode class now resolve? (was 404 before sync/build)
Write-Host "--- HouseGameMode class present? ---"
try {
    $r = Invoke-WebRequest -Uri "$Remote/remote/object/describe" -Method PUT -Body '{"objectPath":"/Script/HouseVictoriaBridge.Default__HouseGameMode"}' -ContentType 'application/json' -UseBasicParsing -TimeoutSec 10
    Write-Host "HOUSEGM-DESC-OK: $($r.StatusCode)"
    $j = $r.Content | ConvertFrom-Json
    Write-Host "  Class=$($j.Class)"
    if ($j.Properties) {
        $j.Properties | Where-Object { $_.Name -match 'DefaultPawn' } | ForEach-Object { Write-Host "  prop: $($_.Name) ($($_.Type))" }
    }
} catch {
    $code = 0; try { $code = [int]$_.Exception.Response.StatusCode.value__ } catch {}
    Write-Host "HOUSEGM-DESC-FAIL code=$code — plugin not compiled yet (sync/LiveCode/restart incomplete)"
}

# 3. Read HouseGameMode CDO DefaultPawnClass (should be KayleighPlayerCharacter, not DefaultPawn)
Write-Host "--- HouseGameMode CDO DefaultPawnClass ---"
try {
    $r = Invoke-WebRequest -Uri "$Remote/remote/object/property" -Method PUT -Body '{"objectPath":"/Script/HouseVictoriaBridge.Default__HouseGameMode","propertyName":"DefaultPawnClass"}' -ContentType 'application/json' -UseBasicParsing -TimeoutSec 10
    Write-Host "PROP-OK: $($r.Content)"
} catch { Write-Host "PROP-FAIL: $($_.Exception.Message)" }

# 4. Confirm KayleighPlayerCharacter class resolves
Write-Host "--- KayleighPlayerCharacter class present? ---"
try {
    $r = Invoke-WebRequest -Uri "$Remote/remote/object/describe" -Method PUT -Body '{"objectPath":"/Script/HouseVictoriaBridge.Default__KayleighPlayerCharacter"}' -ContentType 'application/json' -UseBasicParsing -TimeoutSec 10
    Write-Host "KAYLEIGH-DESC-OK: $($r.StatusCode) Class=$($r.Content)"
} catch {
    $code = 0; try { $code = [int]$_.Exception.Response.StatusCode.value__ } catch {}
    Write-Host "KAYLEIGH-DESC-FAIL code=$code"
}

# 5. Start PIE (no-arg, known to work) and confirm InPIE
Write-Host "--- start PIE ---"
try {
    $r = Invoke-WebRequest -Uri "$Remote/remote/object/call" -Method PUT -Body '{"objectPath":"/Script/LevelEditor.Default__LevelEditorSubsystem","functionName":"EditorRequestBeginPlay","generateTransaction":false}' -ContentType 'application/json' -UseBasicParsing -TimeoutSec 15
    Write-Host "BEGINPLAY-OK: $($r.StatusCode) $($r.Content)"
} catch { Write-Host "BEGINPLAY-FAIL: $($_.Exception.Message)" }

Start-Sleep -Seconds 3
try {
    $r = Invoke-WebRequest -Uri "$Remote/remote/object/call" -Method PUT -Body '{"objectPath":"/Script/LevelEditor.Default__LevelEditorSubsystem","functionName":"IsInPlayInEditor","generateTransaction":false}' -ContentType 'application/json' -UseBasicParsing -TimeoutSec 10
    Write-Host "ISPIE: $($r.Content)"
} catch { Write-Host "ISPIE-FAIL: $($_.Exception.Message)" }

Write-Host ""
Write-Host "=== If DefaultPawnClass contains 'Kayleigh' and not 'DefaultPawn'/'Victoria' -> PASS ==="
Write-Host "=== Then update docs/agents/reports/TASK-20260817-191-REX01-to-PM01.md status to Pass ==="
