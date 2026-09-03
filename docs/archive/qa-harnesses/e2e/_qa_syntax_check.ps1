# Syntax-check all staged E2E scripts (parse only, do NOT execute guarded logic)
$ErrorActionPreference = 'Stop'
$dir = 'C:\Users\kurtw\Soul_Core\SoulCore\scripts\e2e'
$files = @(
    'e2e-harness-common.ps1',
    'e2e-E1-speak.ps1',
    'e2e-E2-set-emotion.ps1',
    'e2e-E3-loco.ps1',
    'e2e-E4-emotion-strip.ps1',
    'e2e-E5-unreal-status.ps1',
    'e2e-E6-want-strip.ps1'
)
foreach ($f in $files) {
    $p = Join-Path $dir $f
    $errs = $null
    $null = [System.Management.Automation.PSParser]::Tokenize((Get-Content -Raw $p), [ref]$errs)
    if ($errs.Count -gt 0) {
        Write-Output ("PARSE FAIL: " + $f)
        foreach ($e in $errs) { Write-Output ("  " + $e.Message + " @ line " + $e.Token.StartLine) }
    } else {
        Write-Output ("PARSE OK:   " + $f)
    }
}
