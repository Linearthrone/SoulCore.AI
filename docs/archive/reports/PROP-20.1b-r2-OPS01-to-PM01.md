---
type: report
prop_id: PROP-20.1b-r2
prop_root: PROP-20-victoria-mind-path
from: OPS-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
title: Interim Debug Host+Presence — identity probe + migration 012
supersedes: PROP-20.1b
also_covers: PROP-20.3b interim
machine: home-pc (KAIA-REIMAGINED)
configuration: Debug
not: Release production
---

# PROP-20.1b-r2 — OPS Debug interim deploy

**Result: Pass** (Debug Wave A smoke only — **not** Release production)

No ImageSharp / license product code patched. Release remains blocked on SixLabors (PROP-20.1c / BED).

## Acceptance

| Criterion | Result |
| --- | --- |
| Host + Presence up on **Debug** | **Pass** — Host `:7710` Debug + ChatDesktop Debug |
| `GET /settings/identity` reachable | **Pass** — HTTP 200; `anchors` + `identityAnchors` present |
| Evidence for schema `012` | **Pass** — `schema_migrations` row `012 / thoughts_wants_dreams_journals` |
| Report with command evidence | **Pass** — this file |
| Release production claim | **N/A / refused** — Debug interim only |

## Commands

```powershell
# First ALLSTART hit DLL file-lock (stale Host). Cleared Host/Presence, then:
dotnet build SoulCore\SoulCore.Host\SoulCore.Host.csproj -c Debug
.\ALLSTART.ps1 -Configuration Debug -RestartHost -SkipTailscaleServe -SkipVoice -SkipBrowserBridge -SkipPlaywrightInstall
# Presence reattached after Host PID refresh:
dotnet run --project House\House.ChatDesktop\House.ChatDesktop.csproj -c Debug --no-build
```

## Evidence

### Host health (`GET http://127.0.0.1:7710/health`)

```text
status=ok
service=SoulCore.Host
port=7710
memory.open=True
memory.path=C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db
```

Host process: `dotnet … SoulCore.Host\bin\Debug\net8.0\SoulCore.Host.dll --urls http://127.0.0.1:7710` (PID 20844).
Log: `SoulCore.Host listening on http://127.0.0.1:7710`.

Debug build succeeded with ImageSharp **warnings** only (no Release license error):

```text
Build succeeded.
… No Six Labors license found. This Debug build can continue, but Release builds require a valid license.
0 Error(s)
```

### Presence

```text
House.ChatDesktop.exe (Debug) running; HOUSE_SOULCORE_PORT=7710
```

### `GET /settings/identity`

```text
HTTP 200
top-level keys: displayName, contactId, charter, identityAnchors, anchors, note
displayName: Victoria
contactId: victoria
charter: {"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"}
identityAnchors: array len=8
anchors: array len=18
note: Read-only charter/identity anchors from SoulCore SQLite. Display name from Companion options (Victoria).
```

Item shape (both `anchors[]` and `identityAnchors[]`):

```text
id, kind, title, body, priority, isLocked, source
```

Example identityAnchors entry (truncated body):

```json
{"id":11,"kind":"identity","title":"Name","body":"I am Victoria.","priority":10,"isLocked":true,"source":"imported"}
```

### Memory migration 012 (PROP-20.3b interim)

DB: `C:\Users\kurtw\AppData\Local\SoulCore\memory\soulcore_memory.db`

`SELECT version, name FROM schema_migrations ORDER BY CAST(version AS INTEGER);`

```text
001	initial
002	embedding_vectors
003	victoria_tasks
004	victoria_workflows
005	episodic_source_model
006	victoria_journals
007	chat_transcript
008	victoria_tasks_park
009	victoria_sops
010	life_hacks_journal
011	victoria_research
012	thoughts_wants_dreams_journals
```

### Ports listening

| Port | Owner | Note |
| --- | --- | --- |
| **7710** | SoulCore.Host (dotnet PID 20844) | Victoria Host (Debug) — healthy |
| 7700 | `tailscaled.exe` (PID 10352) | Not Host; `/health` times out |
| ChatDesktop | `House.ChatDesktop.exe` | Presence GUI (no listen port required) |

## Explicit non-claims

- This is **not** a Release production deploy.
- SixLabors Release gate unchanged; OPS did not add license keys or patch Inference/ImageSharp code.
- `deploy_history.json` / production changelog **not** updated (Debug interim smoke only).

## Handoff

- Wave A Debug desk ready for QA identity smoke against `http://127.0.0.1:7710/settings/identity`.
- Release sign-off still waits on PROP-20.1c (SixLabors license / BED).
