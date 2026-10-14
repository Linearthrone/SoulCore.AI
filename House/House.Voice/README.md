# House.Voice

Local ears and voice for Presence. Both servers live in this repo.

| Service | Port | Where |
|---|---|---|
| Kokoro TTS | `127.0.0.1:8881` | `House/House.Voice/tts/server.py` |
| faster-whisper STT | `127.0.0.1:8000` | `House/House.Voice/stt/server.py` |

The Host posts `{ "text", "voice" }` to the TTS URL and expects a WAV back.
Voice name `default` is Kokoro's `af_heart`. It posts a WAV to STT
`/transcribe` and reads `{ "text" }` back.

## Start

```powershell
.\House\House.Voice\start-tts.ps1
.\House\House.Voice\start-stt.ps1
```

ALLSTART runs both. The first start installs the Python packages and downloads
the models into `House/House.Voice/models` (not committed).

Prefer Python 3.11 at `V:\Python311\python.exe`.
