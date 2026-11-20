---
type: report
prop_id: PROP-21.2
from: BED-01
to: PM-01
status: Pass
created: 2026-11-19
---

# PROP-21.2 — streaming speak + speech format

**Pass.** Chat deltas strip audio tags; Call streams TTS via `voice.speaking` / `voice.audio`; post-effects skip Host speakers during Call. `SonioxSpeechText` guidance in context preamble. Tests: SonioxSpeechText 2/2.
