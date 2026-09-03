# QA-01 F2 .env key-prefix audit — prints only KEY NAMES, never values
$ErrorActionPreference = 'Stop'
$envPath = 'C:\Users\kurtw\Soul_Core\SoulCore\.env'
$examplePath = 'C:\Users\kurtw\Soul_Core\SoulCore\.env.example'

Write-Output '===== .env key names (values redacted) ====='
$lines = Get-Content -Path $envPath
$nonPrefix = @()
$keyCount = 0
foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.Trim().StartsWith('#')) { continue }
    $keyCount++
    $eq = $line.IndexOf('=')
    if ($eq -gt 0) {
        $key = $line.Substring(0, $eq).Trim()
        $valLen = ($line.Substring($eq + 1)).Trim().Length
        $hasPrefix = $key.StartsWith('SOULCORE_')
        Write-Output ("key='{0}' valueLen={1} prefixOK={2}" -f $key, $valLen, $hasPrefix)
        if (-not $hasPrefix) { $nonPrefix += $key }
    }
}
Write-Output ("totalKeys={0} nonPrefixedCount={1}" -f $keyCount, $nonPrefix.Count)
if ($nonPrefix.Count -gt 0) {
    Write-Output ("NON_PREFIXED_KEYS: " + ($nonPrefix -join ', '))
}

Write-Output ''
Write-Output '===== .env.example key names (for structure comparison) ====='
$exLines = Get-Content -Path $examplePath
foreach ($line in $exLines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.Trim().StartsWith('#')) { Write-Output $line; continue }
    $eq = $line.IndexOf('=')
    if ($eq -gt 0) {
        $key = $line.Substring(0, $eq).Trim()
        Write-Output ("key='{0}'" -f $key)
    }
}
