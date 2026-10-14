"""
Local TTS for House Victoria. Speaks the same contract the Host already uses:
POST / with JSON {text, voice?} returns a WAV body. GET / is the health check.

Engine is Kokoro ONNX (in this repo). Model files download into
House/House.Voice/models on first start and are not committed.
"""
from __future__ import annotations

import io
import os
import sys
import wave
from pathlib import Path

import numpy as np
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydantic import BaseModel, Field
import uvicorn

HOST = os.environ.get("HOUSE_TTS_HOST", "127.0.0.1")
PORT = int(os.environ.get("HOUSE_TTS_PORT", "8881"))
DEFAULT_VOICE = os.environ.get("HOUSE_TTS_VOICE", "af_heart")

ROOT = Path(__file__).resolve().parents[1]
MODEL_DIR = Path(os.environ.get("HOUSE_TTS_MODEL_DIR", str(ROOT / "models")))
ONNX_NAME = "kokoro-v1.0.onnx"
VOICES_NAME = "voices-v1.0.bin"
ONNX_URL = (
    "https://github.com/thewh1teagle/kokoro-onnx/releases/download/"
    "model-files-v1.1/kokoro-v1.0.onnx"
)
VOICES_URL = (
    "https://github.com/thewh1teagle/kokoro-onnx/releases/download/"
    "model-files-v1.1/voices-v1.0.bin"
)

# Host still sends voice "default" from the old Chatterbox setting.
VOICE_ALIASES = {
    "default": DEFAULT_VOICE,
    "victoria": DEFAULT_VOICE,
}

_kokoro = None

app = FastAPI(title="House Victoria TTS", version="1.0")


class TtsRequest(BaseModel):
    text: str
    voice: str | None = None
    speed: float | None = Field(default=1.0, ge=0.5, le=2.0)


def _download(url: str, dest: Path) -> None:
    import urllib.request

    dest.parent.mkdir(parents=True, exist_ok=True)
    tmp = dest.with_suffix(dest.suffix + ".part")
    print(f"Downloading {url} -> {dest}", file=sys.stderr, flush=True)
    urllib.request.urlretrieve(url, tmp)
    tmp.replace(dest)


def ensure_models() -> tuple[Path, Path]:
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    onnx = MODEL_DIR / ONNX_NAME
    voices = MODEL_DIR / VOICES_NAME
    if not onnx.is_file() or onnx.stat().st_size < 1_000_000:
        _download(ONNX_URL, onnx)
    if not voices.is_file() or voices.stat().st_size < 1_000_000:
        _download(VOICES_URL, voices)
    return onnx, voices


def get_kokoro():
    global _kokoro
    if _kokoro is not None:
        return _kokoro
    from kokoro_onnx import Kokoro

    onnx, voices = ensure_models()
    print(f"Loading Kokoro from {onnx}", file=sys.stderr, flush=True)
    _kokoro = Kokoro(str(onnx), str(voices))
    return _kokoro


def resolve_voice(voice: str | None) -> str:
    stem = (voice or DEFAULT_VOICE).strip() or DEFAULT_VOICE
    return VOICE_ALIASES.get(stem.lower(), stem)


def split_text(text: str, limit: int = 280) -> list[str]:
    text = " ".join(text.split())
    if len(text) <= limit:
        return [text]
    parts: list[str] = []
    buf = ""
    for piece in text.replace("!", ".").replace("?", ".").split("."):
        sentence = piece.strip()
        if not sentence:
            continue
        candidate = f"{buf} {sentence}".strip() if buf else sentence
        if len(candidate) > limit and buf:
            parts.append(buf)
            buf = sentence
        else:
            buf = candidate
    if buf:
        parts.append(buf)
    return parts or [text[:limit]]


def to_wav(samples: np.ndarray, sample_rate: int) -> bytes:
    audio = np.squeeze(np.asarray(samples, dtype=np.float32))
    if audio.ndim != 1:
        audio = audio.reshape(-1)
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if peak > 1.0:
        audio = audio / peak
    pcm16 = np.clip(audio * 32767.0, -32768, 32767).astype(np.int16)
    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(int(sample_rate))
        wf.writeframes(pcm16.tobytes())
    return buf.getvalue()


@app.get("/")
@app.get("/health")
async def health():
    return {
        "status": "ok",
        "service": "house-kokoro",
        "model_loaded": _kokoro is not None,
        "default_voice": DEFAULT_VOICE,
        "model_dir": str(MODEL_DIR),
    }


@app.post("/")
async def synthesize(request: TtsRequest):
    text = (request.text or "").strip()
    if not text:
        raise HTTPException(status_code=400, detail="Text is required")

    voice = resolve_voice(request.voice)
    speed = request.speed if request.speed is not None else 1.0
    try:
        kokoro = get_kokoro()
        pieces = []
        sample_rate = 24000
        for chunk in split_text(text):
            samples, sample_rate = kokoro.create(
                chunk, voice=voice, speed=speed, lang="en-us"
            )
            pieces.append(np.asarray(samples, dtype=np.float32))
        audio = pieces[0] if len(pieces) == 1 else np.concatenate(pieces)
        wav = to_wav(audio, sample_rate)
    except Exception as exc:
        if voice != DEFAULT_VOICE:
            try:
                kokoro = get_kokoro()
                samples, sample_rate = kokoro.create(
                    text[:280], voice=DEFAULT_VOICE, speed=speed, lang="en-us"
                )
                wav = to_wav(np.asarray(samples, dtype=np.float32), sample_rate)
                return Response(content=wav, media_type="audio/wav")
            except Exception:
                pass
        raise HTTPException(status_code=500, detail=f"Synthesis failed: {exc}") from exc

    return Response(content=wav, media_type="audio/wav")


def main() -> None:
    ensure_models()
    print(f"House TTS on http://{HOST}:{PORT}", file=sys.stderr, flush=True)
    uvicorn.run(app, host=HOST, port=PORT, log_level="info")


if __name__ == "__main__":
    main()
