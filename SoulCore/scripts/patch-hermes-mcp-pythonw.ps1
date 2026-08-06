#Requires -Version 5.1
<#
.SYNOPSIS
  Rewrite Hermes MCP stdio commands from python.exe → pythonw.exe (OPS-178).
.DESCRIPTION
  Blank persistent console windows on ALLSTART come from Hermes spawning MCP
  children with console-subsystem python.exe while the gateway itself is Hidden.
  pythonw.exe (Windows GUI subsystem) keeps the same stdio MCP transport with
  no visible console.

  Safe rules:
    * Only touches YAML lines matching `command:` that end in python.exe
      (not already pythonw.exe).
    * Only rewrites when a sibling pythonw.exe exists next to that python.exe.
    * Writes a timestamped backup: config.yaml.bak-ops178-<stamp>
    * Does NOT disable or remove MCP servers.

  LLMOD quarry (out of tree): Tools\setup-hermes-integration.ps1 should emit
  pythonw.exe for house_victoria (and any other stdio Python MCP) so re-setup
  does not regress. This script (and start-hermes.ps1 preflight) re-applies
  the fix after Hermes reinstall / setup-hermes-integration.

.PARAMETER ConfigPath
  Path to Hermes config.yaml. Default: output of `hermes config path`, else
  %LOCALAPPDATA%\hermes\config.yaml

.PARAMETER WhatIf
  Report rewrites without writing the file.

.OUTPUTS
  Integer count of command lines rewritten (0 if already clean).

.EXAMPLE
  .\patch-hermes-mcp-pythonw.ps1
  .\patch-hermes-mcp-pythonw.ps1 -ConfigPath "$env:LOCALAPPDATA\hermes\config.yaml"
#>
[CmdletBinding()]
param(
    [string]$ConfigPath = "",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Resolve-HermesConfigPath {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path -LiteralPath $Explicit)) { return $Explicit }

    $hermesCmd = Get-Command hermes -ErrorAction SilentlyContinue
    if ($hermesCmd -and $hermesCmd.Source) {
        try {
            $p = (& $hermesCmd.Source config path 2>$null | Out-String).Trim()
            if ($p -and (Test-Path -LiteralPath $p)) { return $p }
        } catch { }
    }

    $fallback = Join-Path $env:LOCALAPPDATA "hermes\config.yaml"
    if (Test-Path -LiteralPath $fallback) { return $fallback }
    return $null
}

function Test-PythonwSibling {
    param([string]$PythonExePath)
    if ([string]::IsNullOrWhiteSpace($PythonExePath)) { return $false }
    $normalized = $PythonExePath.Trim().Trim('"', "'")
    $pythonw = $normalized -replace '(?i)python\.exe$', 'pythonw.exe'
    if ($pythonw -eq $normalized) { return $false }
    # Windows paths in config may use forward slashes.
    $fsPath = $pythonw -replace '/', '\'
    return (Test-Path -LiteralPath $fsPath)
}

function Repair-HermesMcpPythonCommands {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [switch]$DryRun
    )

    $rawLines = Get-Content -LiteralPath $Path -Encoding utf8
    $changed = 0
    $rewrites = New-Object System.Collections.Generic.List[string]
    $outLines = New-Object System.Collections.Generic.List[string]

    foreach ($line in $rawLines) {
        $newLine = $line
        if ($line -match '^\s*command:\s*') {
            # Extract unquoted path for existence check.
            $cmdPart = $line -replace '^\s*command:\s*', ''
            $cmdPath = $cmdPart.Trim().Trim('"', "'")
            $isConsolePython = $cmdPath -match '(?i)(^|[\\/])python\.exe$'
            $alreadyHidden = $cmdPath -match '(?i)(^|[\\/])pythonw\.exe$'
            if ($isConsolePython -and -not $alreadyHidden) {
                if (Test-PythonwSibling -PythonExePath $cmdPath) {
                    $newLine = $line -replace '(?i)(?<!w)python\.exe', 'pythonw.exe'
                    if ($newLine -ne $line) {
                        $changed++
                        $to = $cmdPath -replace '(?i)python\.exe$', 'pythonw.exe'
                        [void]$rewrites.Add("$cmdPath → $to")
                    }
                } else {
                    Write-Warning "OPS-178: pythonw.exe sibling missing; left console python: $cmdPath"
                }
            }
        }
        [void]$outLines.Add($(if ($DryRun) { $line } else { $newLine }))
    }

    if ($changed -gt 0) {
        if ($DryRun) {
            Write-Host "OPS-178: Would rewrite $changed MCP command(s) python.exe → pythonw.exe:"
        } else {
            $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $bak = "$Path.bak-ops178-$stamp"
            Copy-Item -LiteralPath $Path -Destination $bak -Force
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllLines($Path, $outLines.ToArray(), $utf8NoBom)
            Write-Host "OPS-178: Rewrote $changed MCP command(s) python.exe → pythonw.exe"
            Write-Host "OPS-178: Backup: $bak"
        }
        foreach ($r in $rewrites) {
            Write-Host "  $r"
        }
    } else {
        Write-Host "OPS-178: No MCP python.exe commands to rewrite (already pythonw or none)."
    }

    return $changed
}

$config = Resolve-HermesConfigPath -Explicit $ConfigPath
if (-not $config) {
    throw "Hermes config.yaml not found. Pass -ConfigPath or restore via LLMOD Tools\setup-hermes-integration.ps1"
}

Write-Host "OPS-178: Config: $config"
$n = Repair-HermesMcpPythonCommands -Path $config -DryRun:$WhatIf
# Emit count as pipeline output for start-hermes.ps1.
Write-Output $n
