"""
Local speech-to-text for House Victoria. Same contract the Host already uses:
POST /transcribe with multipart field "audio" or "file" returns {"text": "..."}.
GET /health is the health check.

Engine is faster-whisper. The model downloads into House/House.Voice/models
on the first transcription and is not committed.
"""
from __future__ import annotations

import os
from io import BytesIO
from pathlib import Path

from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.responses import JSONResponse
import uvicorn

HOST = os.environ.get("HOUSE_STT_HOST", "127.0.0.1")
PORT = int(os.environ.get("HOUSE_STT_PORT", "8000"))
MODEL_NAME = os.environ.get("WHISPER_MODEL", "base")
DEVICE = os.environ.get("WHISPER_DEVICE", "cpu")
COMPUTE_TYPE = os.environ.get("WHISPER_COMPUTE_TYPE", "int8")
LANGUAGE = os.environ.get("HOUSE_STT_LANGUAGE", "en")

ROOT = Path(__file__).resolve().parents[1]
MODEL_DIR = Path(os.environ.get("HOUSE_STT_MODEL_DIR", str(ROOT / "models" / "whisper")))

_model = None

app = FastAPI(title="House Victoria STT", version="1.0")


def get_model():
    global _model
    if _model is not None:
        return _model
    from faster_whisper import WhisperModel

    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Loading faster-whisper '{MODEL_NAME}' into {MODEL_DIR}", flush=True)
    _model = WhisperModel(
        MODEL_NAME,
        device=DEVICE,
        compute_type=COMPUTE_TYPE,
        download_root=str(MODEL_DIR),
    )
    return _model


@app.get("/health")
def health():
    return {
        "status": "ok",
        "service": "house-whisper",
        "model": MODEL_NAME,
        "model_loaded": _model is not None,
        "model_dir": str(MODEL_DIR),
    }


@app.post("/transcribe")
async def transcribe(
    audio: UploadFile | None = File(None),
    file: UploadFile | None = File(None),
):
    upload = audio or file
    if upload is None:
        raise HTTPException(status_code=400, detail="Missing multipart field 'audio' or 'file'")
    data = await upload.read()
    if not data:
        return JSONResponse(content={"text": ""})
    try:
        model = get_model()
        segments, _info = model.transcribe(
            BytesIO(data),
            language=LANGUAGE or None,
            vad_filter=True,
        )
        text = " ".join(segment.text for segment in segments).strip()
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Transcription failed: {exc}") from exc
    return JSONResponse(content={"text": text})


def main() -> None:
    print(f"House STT on http://{HOST}:{PORT}", flush=True)
    uvicorn.run(app, host=HOST, port=PORT, log_level="info")


if __name__ == "__main__":
    main()
