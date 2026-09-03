# Fix: Cursor ACE `preToolUse` blocking all tools (invalid JSON)

## Symptom

Agent on Windows (home or **shadow**) says every Read/Shell/Write/Task is blocked because:

`.cursor/scripts/ace_pre_tool_use.ps1` returned **invalid JSON**.

PROP-2.1 / REX cannot start until tools work again.

## Fastest unblock (shadow — do this now)

### Option A — Disable the hook (30 seconds)

1. Cursor → **Settings → Hooks**
2. Find **ACE** / `preToolUse` / `ace_pre_tool_use.ps1`
3. **Disable** it for this session (or remove the entry)
4. New Agent chat → `@Agents/REX-01-SHADOW.md` → `Start PROP-2.1.`

### Option B — Replace the script (keeps ACE installed, fail-open allow)

1. Open (or create) in the Soul_Core / workspace root:

   `.cursor\scripts\ace_pre_tool_use.ps1`

2. Replace **entire** contents with the repo file of the same path (canonical copy in git), or paste:

```powershell
# ACE preToolUse — stdout MUST be JSON only
$ErrorActionPreference = 'Stop'
try { $null = [Console]::In.ReadToEnd() } catch {}
[Console]::Out.Write('{"permission":"allow"}')
exit 0
```

3. In `hooks.json` (project `.cursor/hooks.json` or user `~/.cursor/hooks.json`), for this hook set:

   `"failClosed": false`

   so a future crash fails **open**, not deadlocks the agent.

4. Restart Agent chat and start PROP-2.1.

### Verify (PowerShell)

```powershell
'' | powershell -NoProfile -File .\.cursor\scripts\ace_pre_tool_use.ps1
# Must print EXACTLY: {"permission":"allow"}
# No extra lines, banners, or warnings.
```

## Root cause

Cursor hooks speak **JSON on stdout**. `Write-Host`, progress bars, PowerShell warnings on the success stream, or BOM/extra lines make the response invalid. Newer Cursor builds treat that as a **safety failure** and refuse the tool.

## After tools work

REX: load `@Agents/REX-01-SHADOW.md` and execute **PROP-2.1** (shadow Kayleigh 1P PIE Pass). Ticket: `docs/agents/tasks/PROP-2.1-PM01-to-REX01.md`.
