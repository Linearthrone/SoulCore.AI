# House.Voice

Thin launchers for Victoria's local ears + voice:

| Service | Port | Quarry |
|---|---|---|
| faster-whisper STT | `127.0.0.1:8000` | `C:\Users\kurtw\LLMOD\LLMOD-max-master\STTServer` |
| Chatterbox TTS | `127.0.0.1:8881` | `...\ChatterboxServer` + `Media\ChatterboxVoices` |

## Start

```powershell
.\House\House.Voice\start-stt.ps1
.\House\House.Voice\start-tts.ps1
```

Prefer Python 3.11 at `V:\Python311\python.exe` (scripts auto-resolve).

## Chatterbox deps (one-time)

If TTS fails to start:

```powershell
V:\Python311\python.exe -m pip install -r C:\Users\kurtw\LLMOD\LLMOD-max-master\ChatterboxServer\requirements.txt
```

(`chatterbox-tts` + `torch` are large; first CUDA start can take minutes.)

## SoulCore

Host proxies:

- `POST /api/stt` — multipart audio → `{ text }`
- `GET /api/voice/last.wav` — last Chatterbox clip
- `GET /api/voice/health` — STT/TTS reachability

ChatDesktop Voice/Video tab: Hold to talk → STT → chat.send. Replies play on PC speakers; UE plays the same WAV when PIE is up (after bridge rebuild).
