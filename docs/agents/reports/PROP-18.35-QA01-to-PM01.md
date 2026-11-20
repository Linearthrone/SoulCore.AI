---
type: report
prop_id: PROP-18.35
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
depends_on: PROP-18.6c
title: Live QA smoke — residue APIs + B3 budget on :7710
---

# PROP-18.35 — QA report (live Host smoke)

## Verdict: **Pass**

Live `http://127.0.0.1:7710` residue APIs return HTTP 200; `/health` shows `status=ok` with B3 budget `advancesCap=3` / `toolMinutesCap=45`. `:7700` remains foreign/stale (noted only; not a Fail).

**Ready for SLOP-01** (post-QA slop audit).

## Upstream

- OPS Pass: `docs/agents/reports/PROP-18.6c-OPS01-to-PM01.md` (Release Host rebuild on `:7710`)

## Checklist

| # | Check | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| 1 | `GET /health` | status ok; `idleParkedBudget.advancesCap=3`; `toolMinutesCap=45` | ok; advancesCap=3; toolMinutesCap=45 | Pass |
| 2 | `GET /activities?limit=3` | 200 | 200 `{"count":0,"activities":[]}` | Pass |
| 3 | `GET /journals/topic?subject=optics&limit=3` | 200 | 200 empty entries | Pass |
| 4 | `GET /presence/while-you-were-gone?limit=3` | 200 | 200 empty presence | Pass |
| 5 | Probe `:7700` | Note foreign/stale; do not Fail | Responds but stale (`version=0.1.52`, no `idleParkedBudget`) | Noted |

## Real HTTP output (paste)

### 1) GET http://127.0.0.1:7710/health

```text
HTTP 200
{"status":"ok","service":"SoulCore.Host","version":"1.0.0","bind":"127.0.0.1","port":7710,"phase":1,"tasks":{"count":0,"runningId":null},"idleParkedBudget":{"localDay":"2026-11-19","advancesUsed":0,"advancesCap":3,"toolMinutesUsed":0,"toolMinutesCap":45,"exhausted":false,"activeTaskId":null,"exhaustReason":null},"ws":{"path":"/ws","url":"ws://127.0.0.1:7710/ws"},"memory":{"open":true,"path":"C:\\Users\\kurtw\\AppData\\Local\\SoulCore\\memory\\soulcore_memory.db"},"inference":{"enabled":true,"provider":"ollama-cloud","model":"deepseek-v4.1-flash:cloud","cloud":true,"baseUrl":"https://ollama.com","embeddingsEnabled":true,"embeddingModel":"nomic-embed-text","embeddingBaseUrl":"loopback","apiKeyConfigured":true,"visionSidecar":true,"visionModel":"syn:large:vision","visionApiKeyConfigured":true},"soulLoop":{"enabled":true,"tickIntervalSeconds":60},"unreal":{"enabled":true,"target":"ws://house-victoria:8888","connected":false},"presence":{"currentActivity":"Between talks · quiet","activitySource":"tool","activityUpdatedAt":"2026-11-19T10:30:29.3264348+00:00","onHerMind":"Reading about neural interfaces · local"},"tools":{"allowDesktopCapture":true,"allowBrowserCapture":true,"allowComputerControl":true,"softCursorRestore":true,"vmEmbedPane":true,"allowMt4Read":false,"allowMt4Trade":false,"allowEmailRead":false,"allowEmailSend":false,"allowEmailDelete":false,"allowGuestSudo":false,"desktopBackend":"cua","browserBackend":"native","mt4Backend":"llmod","desktopTargetWindowTitle":"KsDevice","cuaDriverAvailable":true,"cuaDriverPath":"C:\\Users\\kurtw\\AppData\\Local\\Programs\\Cua\\cua-driver\\bin\\cua-driver.exe","scope":"session","note":"Session gates until Host restart. Seeded from Tools in appsettings.json. Presence Settings → Tools & Access → Embed VirtualBox in Her screen toggles VmEmbedPane (KsDevice HWND)."},"charter":{"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"},"safety":{"drift":{"activeDriftCount":0,"sloExceeded":false,"oldestDriftMinutes":0},"spend":{"totalTokensIn":0,"totalTokensOut":0,"estimatedCostUsd":0,"monthlyCapUsd":30,"capExceeded":false}},"sms":{"allowlistConfigured":false,"allowlistCount":0,"victoriaMdnConfigured":true,"victoriaMdnLength":12,"outboundEnabled":true,"autoReplyEnabled":true,"inboundUsesToolLoop":true}}
```

B3 budget fields verified from body:

- `idleParkedBudget.advancesCap` = **3**
- `idleParkedBudget.toolMinutesCap` = **45**
- `status` = **ok**
- `port` = **7710**

### 2) GET http://127.0.0.1:7710/activities?limit=3

```text
HTTP 200
{"count":0,"activities":[]}
```

### 3) GET http://127.0.0.1:7710/journals/topic?subject=optics&limit=3

```text
HTTP 200
{"subject":"optics","bookId":"thoughts","count":0,"entries":[]}
```

### 4) GET http://127.0.0.1:7710/presence/while-you-were-gone?limit=3

```text
HTTP 200
{"since":"2026-11-18T22:30:41.541Z","empty":true,"activities":[],"activityCount":0,"journalCrumbs":[],"journalCount":0}
```

### 5) Note probe — GET http://127.0.0.1:7700/health (not a Fail)

```text
HTTP 200
{"status":"ok","service":"SoulCore.Host","version":"0.1.52","bind":"127.0.0.1","port":7700,"phase":1,"tasks":{"count":0,"runningId":null},"ws":{"path":"/ws","url":"ws://127.0.0.1:7700/ws"},"memory":{"open":true,"path":"C:\\Users\\kurtw\\AppData\\Local\\SoulCore\\memory\\soulcore_memory.db"},"inference":{"enabled":true,"provider":"ollama","model":"gemma4:latest","cloud":false,"baseUrl":"loopback","embeddingsEnabled":true,"embeddingModel":"nomic-embed-text","embeddingBaseUrl":"loopback","apiKeyConfigured":true,"visionSidecar":true,"visionModel":"syn:large:vision","visionApiKeyConfigured":true},"soulLoop":{"enabled":true,"tickIntervalSeconds":60},"unreal":{"enabled":true,"target":"ws://house-victoria:8888","connected":false},"presence":{"currentActivity":"Between talks · quiet","activitySource":"tool","activityUpdatedAt":"2026-11-19T10:30:10.1985568+00:00","onHerMind":"Reading about neural interfaces · local"},"tools":{"allowDesktopCapture":true,"allowBrowserCapture":true,"allowComputerControl":true,"softCursorRestore":true,"vmEmbedPane":true,"allowMt4Read":false,"allowMt4Trade":false,"allowEmailRead":false,"allowEmailSend":false,"allowEmailDelete":false,"allowGuestSudo":false,"desktopBackend":"cua","browserBackend":"native","mt4Backend":"llmod","desktopTargetWindowTitle":"victoria-sandbox","cuaDriverAvailable":true,"cuaDriverPath":"C:\\Users\\kurtw\\AppData\\Local\\Programs\\Cua\\cua-driver\\bin\\cua-driver.exe","scope":"session","note":"Session gates until Host restart. Seeded from Tools in appsettings.json. Presence Settings → Tools & Access → Embed VirtualBox in Her screen toggles VmEmbedPane (victoria-sandbox HWND)."},"charter":{"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"},"safety":{"drift":{"activeDriftCount":0,"sloExceeded":false,"oldestDriftMinutes":0},"spend":{"totalTokensIn":0,"totalTokensOut":0,"estimatedCostUsd":0,"monthlyCapUsd":30,"capExceeded":false}},"sms":{"allowlistConfigured":false,"allowlistCount":0,"victoriaMdnConfigured":true,"victoriaMdnLength":12,"outboundEnabled":true,"autoReplyEnabled":true,"inboundUsesToolLoop":false}}
```

`:7700` note: still listening but foreign/stale vs Victoria on `:7710` — older `version=0.1.52`, no `idleParkedBudget` block, local Ollama/`gemma4` vs cloud on `:7710`. Per task: do not Fail.

## Issues

None opened. Empty residue payloads are acceptable per OPS 18.6c acceptance (non-404 / HTTP 200).

## Method

PowerShell `Invoke-WebRequest` against live loopback Host (no code changes, no deploy).

QA-01 | PROP-18.35 | 2026-11-19
