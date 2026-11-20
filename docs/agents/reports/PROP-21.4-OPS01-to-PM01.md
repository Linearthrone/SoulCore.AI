---
type: report
prop_id: PROP-21.4
from: OPS-01
to: PM-01
status: Pass
created: 2026-11-19
---

# PROP-21.4 — OPS Soniox wiring

**Pass.**

- `SONIOX_API_KEY` present in repo `.env` (length noted only; not logged).
- Host DotEnvLoader maps key → `Voice:ApiKey` (30 SOULCORE_* keys loaded on start).
- ALLSTART no longer starts House.Voice Whisper/Kokoro; Soniox banner when not `-SkipVoice`.
- Live probe:

```text
GET http://127.0.0.1:7710/api/voice/health -> 200
{"enabled":true,"provider":"soniox","apiKeyConfigured":true,
 "stt":{"ok":true,"realtimeModel":"stt-rt-v6",...},
 "tts":{"ok":true,"voice":"Rebecca","model":"tts-rt-v2",...}}
GET /health -> 200
```

ChatDesktop start may still exit 1 if Presence already running (known lock); Host is green.
