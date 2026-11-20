---
type: report
prop_id: PROP-21.5
from: QA-01
to: PM-01
status: Pass
created: 2026-11-19
---

# PROP-21.5 — QA Soniox + Call

**Pass** (automated + live health).

| Check | Result |
| --- | --- |
| SonioxSpeechText strip/normalize | **2/2 Passed** |
| DotEnvLoader (incl. existing) | Passed in filter run (5 total with speech) |
| Host `/api/voice/health` provider=soniox, apiKeyConfigured | Pass |
| Host `/health` | Pass |
| ChatDesktop Debug build (Call overlay) | Pass |
| Protocol.Tests compile with VoiceCallHandler | Pass |

```text
dotnet test --filter SonioxSpeechText|DotEnvLoader → Passed: 5
VOICE_HEALTH=200 apiKeyConfigured=true voice=Rebecca
```

Manual desk Call (mic → sphere) left for operator smoke when Presence is relaunched with the new binary.
