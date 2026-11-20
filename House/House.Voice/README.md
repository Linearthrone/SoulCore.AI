# House.Voice (deprecated — PROP-21)

Local faster-whisper STT and Kokoro TTS satellites are **no longer used** by Presence / SoulCore.Host.

Voice now goes through **Soniox** (`SONIOX_API_KEY` in repo `.env` → Host `Voice:ApiKey`).

Scripts `start-stt.ps1` / `start-tts.ps1` remain for offline experiments only. ALLSTART skips them by default.
