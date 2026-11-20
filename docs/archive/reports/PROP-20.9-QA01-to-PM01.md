---
type: report
prop_id: PROP-20.9
prop_root: PROP-20-victoria-mind-path
from: QA-01
to: PM-01
priority: P0
status: Pass
created: 2026-11-17
amended: 2026-11-17
depends_on_accept: PROP-20.2, PROP-20.4, PROP-20.5, PROP-20.6, PROP-20.7, PROP-20.8
title: Wave A regression — mind / memory / SMS gate / journals / identity + PROP-20.7 SMS suites
---

# PROP-20.9 — QA-01 Wave A (+ PROP-20.7 SMS) → PM-01

**Verdict: Pass** (Debug Host + unit/integration only). **Not** a Release production Pass (SixLabors / Release Host blocked per task).

**Ready for SLOP-01** (post-QA slop audit).

## Scope executed

| Area | Result |
| --- | --- |
| VictoriaMindBlock / ChatContext / Memory dated / Journals / Leisure / SMS security | **Pass** (100/100) |
| ChatDesktop identity client tests | **Pass** (2/2) |
| PROP-20.7 SmsOutboundComposer + SmsInboundProp207 + inbound/outbound SMS suites | **Pass** (82/82) |
| Host Debug smoke: `/health`, `/settings/identity`, `/journals/books` | **Pass** (HTTP 200; books include thoughts/wants/dreams) |
| SMS denied tools never execute | **Pass** (`SmsToolGate_FilterRegistry_DeniedNeverReachesInner` + deny-list suite) |
| Release Host | **Not claimed** (blocked / out of scope) |

PM accepts referenced: `PROP-20.{2,4,5,6,7,8}-PM01-accept.md`.

## 1) Unit / integration — Wave A core filters

**Command:**

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~VictoriaMindBlockTests|FullyQualifiedName~ChatContextBuilderTests|FullyQualifiedName~MemoryRecallDatedTests|FullyQualifiedName~SqliteMemoryStoreJournalTests|FullyQualifiedName~SmsSecurityGateTests|FullyQualifiedName~Prop17LeisureTests" --verbosity normal
```

**Result (real output):**

```text
Total tests: 100
     Passed: 100
```

| Class | Passed |
| --- | ---: |
| VictoriaMindBlockTests | 4 |
| ChatContextBuilderTests | 10 |
| MemoryRecallDatedTests | 3 |
| SqliteMemoryStoreJournalTests | 4 |
| SmsSecurityGateTests | 57 |
| Prop17LeisureTests | 22 |

**SMS gate denial evidence (suite):**

```text
  Passed SoulCore.Protocol.Tests.SmsSecurityGateTests.SmsToolGate_FilterRegistry_DeniedNeverReachesInner [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsSecurityGateTests.Inbound_HostileSms_DeniedToolsNeverExecute [35 ms]
```

Full log: `tmpcode/prop209-protocol-tests.txt`.

## 2) ChatDesktop identity

First rebuild failed because `House.ChatDesktop` PID locked DLLs (environment, not product). Retried authorized path:

```text
dotnet test House/House.ChatDesktop.Tests/House.ChatDesktop.Tests.csproj --filter "FullyQualifiedName~SoulCoreIdentitySettingsClientTests" --no-build --verbosity normal
```

**Result:**

```text
  Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.FormatAnchorsBlock_EmptyOrError_DoesNotFabricate [104 ms]
  Passed House.ChatDesktop.Tests.SoulCoreIdentitySettingsClientTests.FormatAnchorsBlock_RendersLiveTitlesAndBodies [1 ms]

Test Run Successful.
Total tests: 2
     Passed: 2
```

Log: `tmpcode/prop209-desktop-identity-tests-nobuild.txt`.

## 3) PROP-20.7 amend — SMS composer / inbound Prop207 / full inbound+outbound

**Command (rebuild blocked by Host file lock → `--no-build`):**

```text
dotnet test SoulCore/SoulCore.Protocol.Tests/SoulCore.Protocol.Tests.csproj --filter "FullyQualifiedName~SmsOutboundComposerTests|FullyQualifiedName~SmsInboundProp207Tests|FullyQualifiedName~SmsInboundServiceTests|FullyQualifiedName~SmsOutboundServiceTests|FullyQualifiedName~SmsSecurityGateTests" --no-build --verbosity normal
```

**Result:**

```text
Test Run Successful.
Total tests: 82
     Passed: 82
 Total time: 0.6226 Seconds
EXIT=0
```

| Class | Passed | Notes |
| --- | ---: | --- |
| SmsOutboundComposerTests | 6 | answer+URL, 480 ceiling, strip internal paper URL, reject loopback/private |
| SmsInboundProp207Tests | 3 | public URL on navigate; long findings → research paper not journal; preamble allow-list |
| SmsInboundServiceTests | 8 | inbound service + preamble |
| SmsOutboundServiceTests | 8 | enqueue SMS/MMS, allowlist, rate gap, auto-enqueue |
| SmsSecurityGateTests | 57 | gate + deny never executes |

**PROP-20.7 pasted lines:**

```text
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.Compose_AnswerPlusPublicUrl_Within480 [13 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.IsPublicHttpUrl_RejectsLoopbackAndPrivate [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.Compose_LongAnswer_TruncatesAndFlagsPaper [2 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.CollectPublicUrlsFromToolTrace_TakesBrowserNavigate [3 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.Compose_480HardCeiling [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsOutboundComposerTests.Compose_StripsInternalPaperAddress_KeepsPublicUrl [< 1 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundProp207Tests.Inbound_ToolNavigate_OutboundIncludesPublicUrl [39 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundProp207Tests.Inbound_LongFindings_PersistsResearchPaper_NotJournal [2 ms]
  Passed SoulCore.Protocol.Tests.SmsInboundProp207Tests.BuildSmsPreamble_ToolLoop_HasAllowList_NoPrivateJournals [< 1 ms]
```

Log: `tmpcode/prop209-sms-suites-nobuild.txt`.

Live Host SMS path not exercised (task: suite evidence enough when Host SMS not live).

## 4) Host Debug HTTP probes

Host Debug on `http://127.0.0.1:7710` (PID 39628 `dotnet`). Release not claimed.

**Probes (PowerShell `Invoke-WebRequest`):**

### GET /health → 200

```json
{"status":"ok","service":"SoulCore.Host","version":"0.1.54","bind":"127.0.0.1","port":7710,"memory":{"open":true},"charter":{"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"},"sms":{"allowlistConfigured":false,"allowlistCount":0,"victoriaMdnConfigured":true,"outboundEnabled":true,"autoReplyEnabled":true,"inboundUsesToolLoop":true}}
```

### GET /settings/identity → 200

```json
{"displayName":"Victoria","contactId":"victoria","charter":{"anchors":18,"locked":18,"fullyLocked":true,"mode":"locked"},"identityAnchors":[{"id":11,"title":"Name","body":"I am Victoria."},{"id":15,"title":"Kayleigh",...}],"note":"Read-only charter/identity anchors from SoulCore SQLite. Display name from Companion options (Victoria)."}
```

### GET /journals/books → 200 (thoughts / wants / dreams present)

```json
{"count":7,"books":[
  {"id":"feeling","title":"Feeling Journal",...},
  {"id":"animation","title":"Animation & Expression Journal",...},
  {"id":"environment","title":"Environment Journal",...},
  {"id":"life_hacks","title":"Life-Hacks Journal",...},
  {"id":"thoughts","title":"Thoughts Journal",...},
  {"id":"wants","title":"Wants Journal",...},
  {"id":"dreams","title":"Dreams Journal",...}
]}
```

Full probe dump: `tmpcode/prop209-http-probes2.txt` (also earlier `tmpcode/prop209-http-probes.txt`).

## Issues filed

None. No product Fail observed in Wave A / PROP-20.7 scope.

**Environment notes (not ISSUEs):**

- Rebuilds of Host/ChatDesktop can hit MSB3027 file locks while Debug Host / ChatDesktop are running; QA used `--no-build` to obtain test evidence without modifying product.
- `/health` reports `sms.allowlistConfigured=false` on this Debug instance (fail-closed live SMS); covered by unit suite, not a regression Fail for this ticket.

## Summary for PM

| Gate | Pass/Fail |
| --- | --- |
| Wave A mind/memory/journals/leisure/SMS security | **Pass** |
| PROP-20.7 SMS answer+URL + paper overflow suites | **Pass** |
| Identity desktop unit | **Pass** |
| Host Debug HTTP smoke | **Pass** |
| Release production | **Not Pass / not claimed** |

**Overall PROP-20.9: Pass**
