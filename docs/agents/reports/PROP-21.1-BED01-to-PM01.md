---
type: report
prop_id: PROP-21.1
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
---

# PROP-21.1 — Soniox clients

**Pass.** Whisper/Kokoro clients removed. `SonioxSttClient` (async file), `SonioxTtsClient` (WS), `SonioxRealtimeSttSession`, `VoiceOptions` rewritten. `DotEnvLoader` maps `SONIOX_API_KEY` → `SOULCORE_Voice__ApiKey`. Health reports Soniox provider.
