# SoulCore continuity soak runbook (pre-24h)

Ops gate before claiming continuous self. QA-036 C1–C6 Pass is a prerequisite; this runbook covers always-on Host reliability on loopback only.

**SEC-004:** V1 binds `127.0.0.1` only. Do **not** enable `0.0.0.0`, LAN, or cloud binds for soak.

---

## Endpoints

| Role | URL |
| --- | --- |
| Health | `GET http://127.0.0.1:7700/health` |
| WebSocket | `ws://127.0.0.1:7700/ws` |
| Bind | `127.0.0.1:7700` (refuse non-loopback) |

Healthy `/health` should include `"status":"ok"`, `"bind":"127.0.0.1"`, `"port":7700`, and a `ws.url` of `ws://127.0.0.1:7700/ws`.

---

## Start / stop

From repo root (`Soul_Core`):

```powershell
# Start (builds Debug Host DLL if missing; loopback only)
.\SoulCore\scripts\start-soulcore.ps1

# Stop (only kills listeners on 127.0.0.1:7700)
.\SoulCore\scripts\stop-soulcore.ps1
```

| Artifact | Path |
| --- | --- |
| PID file | `SoulCore/scripts/.soulcore-host.pid` |
| Stdout log | `SoulCore/scripts/.soulcore-host.log` |
| Stderr log | `SoulCore/scripts/.soulcore-host.log.err` |
| Memory DB | `%LOCALAPPDATA%\SoulCore\memory\soulcore_memory.db` |

Quick health:

```powershell
Invoke-WebRequest http://127.0.0.1:7700/health -UseBasicParsing
```

Listen table (must stay loopback):

```powershell
Get-NetTCPConnection -LocalPort 7700 -State Listen |
  Select-Object LocalAddress, LocalPort, OwningProcess
# Expect LocalAddress = 127.0.0.1 only
```

---

## Short soak (default 15 minutes)

Optional probe script (not a full 24h yet):

```powershell
.\SoulCore\scripts\soak-soulcore.ps1              # 15 min default
.\SoulCore\scripts\soak-soulcore.ps1 -Minutes 15  # explicit
.\SoulCore\scripts\soak-soulcore.ps1 -Minutes 5   # shorter smoke
```

Behavior:

- Ensures Host is up (calls `start-soulcore.ps1` if `:7700` not listening).
- Probes `/health` on an interval (default 15s).
- Records OwningProcess PID each probe; flags PID changes.
- Writes a soak log under `SoulCore/scripts/logs/`.
- Exits non-zero on abort criteria (below).

---

## Confirm House still connects after Host restart

1. Start Host: `.\SoulCore\scripts\start-soulcore.ps1`
2. Launch `House/House.ChatDesktop` (connects to `ws://127.0.0.1:7700/ws` only).
3. Confirm UI shows presence / can chat (or WS handshake frames).
4. Stop Host: `.\SoulCore\scripts\stop-soulcore.ps1`
5. Confirm House shows disconnect / cannot complete chat.
6. Start Host again: `.\SoulCore\scripts\start-soulcore.ps1`
7. Confirm House reconnects (presence + emotion snapshot handshake) without pointing House at Ollama/Hermes directly.

PowerShell-only handshake check (no UI):

```powershell
# After restart: ClientWebSocket to ws://127.0.0.1:7700/ws should receive
# presence.status + emotion.snapshot (see QA-036 C1/C3 evidence pattern).
```

---

## Log locations

| What | Where |
| --- | --- |
| Host stdout / stderr (script-started) | `SoulCore/scripts/.soulcore-host.log` (+ `.err`) |
| Soak probe log | `SoulCore/scripts/logs/soak-YYYYMMDD-HHmmss.log` |
| Memory SQLite | `%LOCALAPPDATA%\SoulCore\memory\soulcore_memory.db` |
| Agent OPS reports | `docs/agents/reports/TASK-*-OPS01-to-PM01.md` |

---

## Abort criteria (fail the soak)

Stop and treat as **Fail** if any of the following occur:

| Criterion | Signal |
| --- | --- |
| Crash loop | Host exits repeatedly; start script cannot keep `:7700` listening; ≥3 unexpected process exits in the soak window |
| Port steal | Listener on `:7700` is not `127.0.0.1`, or OwningProcess changes without an intentional stop/start |
| Disk full / memory DB | `/health` reports `"memory":{"open":false}` or SQLite open errors in Host log; volume free space &lt; 200 MB on `%LOCALAPPDATA%` drive |
| Health fail streak | Consecutive `/health` failures exceed threshold (script default: 3) |
| Non-loopback bind | Any listen on `0.0.0.0:7700` or health `"bind"` ≠ `127.0.0.1` |

---

## 24h soak

**Authorized** by product owner 2026-07-23 (OPS-059). When running:

1. Start Host on a stable machine (keep lid awake / power settings).
2. Run `.\SoulCore\scripts\soak-soulcore.ps1 -Minutes 1440` (or equivalent scheduled probe).
3. Optionally keep House connected; note reconnects after any Host restart.
4. Archive soak log + PID timeline + final `/health` JSON into an OPS report.
5. Still loopback-only — no LAN expose.

---

## Related

- Continuity suite: QA-036 C1–C6
- Product root: `docs/agents/PRODUCT_ROOT.md`
- Local start/stop: `SoulCore/README.md` (Local start / stop)
