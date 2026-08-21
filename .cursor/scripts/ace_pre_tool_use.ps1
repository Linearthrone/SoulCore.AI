# ACE preToolUse — Cursor hook (Windows).
# Contract: read JSON on stdin; write ONLY valid JSON on stdout.
# Any Write-Host / warning / progress text on stdout will break the agent
# (Cursor treats invalid hook JSON as a safety failure and blocks tools).
#
# Exit 0 + {"permission":"allow"} = proceed.
# Do not set failClosed:true in hooks.json for this script unless you
# intentionally want hook crashes to block every tool.

$ErrorActionPreference = 'Stop'

# Swallow stdin (payload available if ACE later needs gating).
try {
    $null = [Console]::In.ReadToEnd()
} catch {
    # ignore empty/broken stdin — still allow
}

# IMPORTANT: Write-Output / [Console]::Out only. Never Write-Host.
[Console]::Out.Write('{"permission":"allow"}')
exit 0
