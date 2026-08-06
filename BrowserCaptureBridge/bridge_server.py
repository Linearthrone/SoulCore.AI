"""Local HTTP + WebSocket bridge between the HV browser extension and MCP/HV.

- HTTP :17891 — MCP on-demand capture (POST /capture), actions (POST /action), health, poll
- WebSocket ws://127.0.0.1:17891/ws/cast — live tab cast (extension producer → HV consumer)
"""

from __future__ import annotations

import asyncio
import base64
import time
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import Response
from pydantic import BaseModel, Field
import uvicorn

DEFAULT_PORT = 17891
JOB_TIMEOUT_SECONDS = 35.0
POLL_INTERVAL_SECONDS = 0.1

app = FastAPI(title="House Victoria Browser Capture Bridge")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@dataclass
class BridgeJob:
    job_id: str
    kind: Literal["capture", "action"] = "capture"
    include_screenshot: bool = True
    include_page_map: bool = True
    # Action fields (kind == "action")
    action: str | None = None
    selector: str | None = None
    index: int | None = None
    x: float | None = None
    y: float | None = None
    button: str = "left"
    text: str | None = None
    clear: bool = False
    key: str | None = None
    modifiers: list[str] = field(default_factory=list)
    delta_x: float = 0.0
    delta_y: float = 0.0
    created_at: float = field(default_factory=time.time)
    status: str = "pending"  # pending | claimed | done | expired
    result: dict[str, Any] | None = None


_jobs: dict[str, BridgeJob] = {}
_lock = asyncio.Lock()
_stream_enabled: bool = False
_latest_stream: dict[str, Any] | None = None
_cast_producers: set[WebSocket] = set()
_cast_consumers: set[WebSocket] = set()


class StreamEnableRequest(BaseModel):
    enabled: bool = True


class StreamPushPayload(BaseModel):
    ok: bool = True
    url: str | None = None
    title: str | None = None
    tab_id: int | None = None
    screenshot_base64: str | None = None
    captured_at: float | None = None


class CaptureRequest(BaseModel):
    include_screenshot: bool = True
    include_page_map: bool = True
    timeout_seconds: float = Field(default=JOB_TIMEOUT_SECONDS, ge=5, le=120)


class ActionRequest(BaseModel):
    action: Literal["click", "type", "key", "scroll"]
    selector: str | None = None
    index: int | None = None
    x: float | None = None
    y: float | None = None
    button: Literal["left", "right", "middle"] = "left"
    text: str | None = None
    clear: bool = False
    key: str | None = None
    modifiers: list[str] = Field(default_factory=list)
    delta_x: float = 0.0
    delta_y: float = 0.0
    timeout_seconds: float = Field(default=JOB_TIMEOUT_SECONDS, ge=5, le=120)


class ResultPayload(BaseModel):
    job_id: str
    ok: bool = False
    error: str | None = None
    detail: str | None = None
    kind: str | None = None  # extension should echo "capture" | "action"
    tab_id: int | None = None
    window_id: int | None = None
    url: str | None = None
    title: str | None = None
    screenshot_base64: str | None = None
    page_map: dict[str, Any] | None = None


async def _store_and_broadcast_frame(
    *,
    url: str | None,
    title: str | None,
    tab_id: int | None,
    screenshot_base64: str,
) -> None:
    global _latest_stream
    captured_at = time.time()
    async with _lock:
        _latest_stream = {
            "ok": True,
            "url": url,
            "title": title,
            "tab_id": tab_id,
            "screenshot_base64": screenshot_base64,
            "captured_at": captured_at,
        }

    payload = {
        "type": "frame",
        "url": url,
        "title": title,
        "tab_id": tab_id,
        "png": screenshot_base64,
        "captured_at": captured_at,
    }
    dead: list[WebSocket] = []
    for ws in list(_cast_consumers):
        try:
            await ws.send_json(payload)
        except Exception:
            dead.append(ws)
    for ws in dead:
        _cast_consumers.discard(ws)


@app.get("/health")
async def health() -> dict[str, Any]:
    pending = sum(1 for j in _jobs.values() if j.status == "pending")
    latest_age: float | None = None
    if _latest_stream and _latest_stream.get("captured_at"):
        latest_age = round(time.time() - float(_latest_stream["captured_at"]), 2)
    return {
        "ok": True,
        "service": "hv-browser-capture-bridge",
        "port": DEFAULT_PORT,
        "pending_jobs": pending,
        "stream_enabled": _stream_enabled,
        "latest_frame_age_seconds": latest_age,
        "cast_producers": len(_cast_producers),
        "cast_consumers": len(_cast_consumers),
        "cast_socket": "ws://127.0.0.1:17891/ws/cast",
        "supports_actions": True,
    }


@app.websocket("/ws/cast")
async def cast_socket(websocket: WebSocket) -> None:
    """Live browser tab cast. Extension = producer, House Victoria = consumer."""
    await websocket.accept()
    role: str | None = None
    try:
        hello = await websocket.receive_json()
        role = hello.get("role")
        if role == "producer":
            _cast_producers.add(websocket)
            await websocket.send_json({"type": "hello", "role": "producer", "ok": True})
        elif role == "consumer":
            _cast_consumers.add(websocket)
            await websocket.send_json({"type": "hello", "role": "consumer", "ok": True})
            if _latest_stream and _latest_stream.get("screenshot_base64"):
                await websocket.send_json(
                    {
                        "type": "frame",
                        "url": _latest_stream.get("url"),
                        "title": _latest_stream.get("title"),
                        "tab_id": _latest_stream.get("tab_id"),
                        "png": _latest_stream.get("screenshot_base64"),
                        "captured_at": _latest_stream.get("captured_at"),
                    }
                )
        else:
            await websocket.close(code=1008)
            return

        while True:
            if role == "producer":
                msg = await websocket.receive_json()
                if msg.get("type") != "frame":
                    continue
                png = msg.get("png") or msg.get("screenshot_base64")
                if not png:
                    continue
                await _store_and_broadcast_frame(
                    url=msg.get("url"),
                    title=msg.get("title"),
                    tab_id=msg.get("tab_id"),
                    screenshot_base64=png,
                )
            else:
                # Consumer: keep alive; frames are pushed from the bridge.
                msg = await websocket.receive_text()
                if msg.strip().lower() == "ping":
                    await websocket.send_text("pong")
    except WebSocketDisconnect:
        pass
    except Exception:
        pass
    finally:
        _cast_producers.discard(websocket)
        _cast_consumers.discard(websocket)


@app.post("/stream/enable")
async def stream_enable(request: StreamEnableRequest) -> dict[str, Any]:
    global _stream_enabled, _latest_stream
    async with _lock:
        _stream_enabled = request.enabled
        if not request.enabled:
            _latest_stream = None
    return {"ok": True, "stream_enabled": _stream_enabled}


@app.get("/stream/status")
async def stream_status() -> dict[str, Any]:
    return {
        "ok": True,
        "stream_enabled": _stream_enabled,
        "cast_producers": len(_cast_producers),
        "cast_consumers": len(_cast_consumers),
    }


@app.post("/stream")
async def stream_push(payload: StreamPushPayload) -> dict[str, bool]:
    """Legacy HTTP push — WebSocket producer is preferred."""
    if not _stream_enabled:
        return {"ok": False}
    if not payload.ok or not payload.screenshot_base64:
        return {"ok": False}
    await _store_and_broadcast_frame(
        url=payload.url,
        title=payload.title,
        tab_id=payload.tab_id,
        screenshot_base64=payload.screenshot_base64,
    )
    return {"ok": True}


@app.get("/latest")
async def latest_frame() -> dict[str, Any]:
    if _latest_stream is None:
        return {"ok": False, "error": "no_frame"}
    payload = dict(_latest_stream)
    payload.pop("screenshot_base64", None)
    payload["has_image"] = True
    return payload


@app.get("/latest.png")
async def latest_frame_png() -> Response:
    if _latest_stream is None:
        return Response(status_code=404)
    b64 = _latest_stream.get("screenshot_base64")
    if not b64:
        return Response(status_code=404)
    try:
        raw = base64.b64decode(b64)
    except Exception:
        return Response(status_code=500)
    return Response(content=raw, media_type="image/png")


@app.get("/latest/meta")
async def latest_frame_meta() -> dict[str, Any]:
    if _latest_stream is None:
        return {"ok": False, "error": "no_frame"}
    return {
        "ok": True,
        "url": _latest_stream.get("url"),
        "title": _latest_stream.get("title"),
        "tab_id": _latest_stream.get("tab_id"),
        "captured_at": _latest_stream.get("captured_at"),
        "has_image": bool(_latest_stream.get("screenshot_base64")),
    }


def _poll_payload_for_job(job: BridgeJob) -> dict[str, Any]:
    base: dict[str, Any] = {
        "pending": True,
        "stream_enabled": _stream_enabled,
        "cast_socket": "ws://127.0.0.1:17891/ws/cast",
        "job_id": job.job_id,
        "kind": job.kind,
    }
    if job.kind == "capture":
        base["include_screenshot"] = job.include_screenshot
        base["include_page_map"] = job.include_page_map
        return base
    base.update(
        {
            "action": job.action,
            "selector": job.selector,
            "index": job.index,
            "x": job.x,
            "y": job.y,
            "button": job.button,
            "text": job.text,
            "clear": job.clear,
            "key": job.key,
            "modifiers": list(job.modifiers),
            "delta_x": job.delta_x,
            "delta_y": job.delta_y,
        }
    )
    return base


@app.get("/poll")
async def poll() -> dict[str, Any]:
    async with _lock:
        _expire_stale_jobs()
        for job in _jobs.values():
            if job.status == "pending":
                job.status = "claimed"
                return _poll_payload_for_job(job)
    return {
        "pending": False,
        "stream_enabled": _stream_enabled,
        "cast_socket": "ws://127.0.0.1:17891/ws/cast",
    }


@app.post("/result")
async def post_result(payload: ResultPayload) -> dict[str, bool]:
    async with _lock:
        job = _jobs.get(payload.job_id)
        if not job:
            return {"ok": False}
        job.status = "done"
        job.result = payload.model_dump()
    if _stream_enabled and payload.ok and payload.screenshot_base64:
        await _store_and_broadcast_frame(
            url=payload.url,
            title=payload.title,
            tab_id=payload.tab_id,
            screenshot_base64=payload.screenshot_base64,
        )
    return {"ok": True}


async def _wait_for_job(job: BridgeJob, timeout_seconds: float) -> dict[str, Any]:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        async with _lock:
            if job.status == "done" and job.result is not None:
                result = job.result
                _jobs.pop(job.job_id, None)
                if job.kind == "capture":
                    return _finalize_capture_result(result)
                return _finalize_action_result(result)
            if job.status == "claimed" and time.time() - job.created_at > timeout_seconds:
                break
        await asyncio.sleep(POLL_INTERVAL_SECONDS)

    async with _lock:
        _jobs.pop(job.job_id, None)
    return {
        "ok": False,
        "error": "extension_timeout",
        "hint": "Load the House Victoria Browser Capture extension in Chrome/Edge and ensure this bridge is running.",
    }


@app.post("/capture")
async def capture(request: CaptureRequest) -> dict[str, Any]:
    job_id = str(uuid.uuid4())
    job = BridgeJob(
        job_id=job_id,
        kind="capture",
        include_screenshot=request.include_screenshot,
        include_page_map=request.include_page_map,
    )
    async with _lock:
        _jobs[job_id] = job
    return await _wait_for_job(job, request.timeout_seconds)


@app.post("/action")
async def action(request: ActionRequest) -> dict[str, Any]:
    job_id = str(uuid.uuid4())
    job = BridgeJob(
        job_id=job_id,
        kind="action",
        include_screenshot=False,
        include_page_map=False,
        action=request.action,
        selector=request.selector,
        index=request.index,
        x=request.x,
        y=request.y,
        button=request.button,
        text=request.text,
        clear=request.clear,
        key=request.key,
        modifiers=list(request.modifiers),
        delta_x=request.delta_x,
        delta_y=request.delta_y,
    )
    async with _lock:
        _jobs[job_id] = job
    return await _wait_for_job(job, request.timeout_seconds)


def _expire_stale_jobs(max_age_seconds: float = 120.0) -> None:
    now = time.time()
    stale = [jid for jid, j in _jobs.items() if now - j.created_at > max_age_seconds]
    for jid in stale:
        _jobs.pop(jid, None)


def _finalize_action_result(result: dict[str, Any]) -> dict[str, Any]:
    # Reject capture-shaped replies from older extensions that ignore kind=action.
    if result.get("kind") != "action":
        return {
            "ok": False,
            "error": "extension_outdated",
            "detail": "Extension handled the job as a capture. Reload unpacked extension to 1.3.0+.",
            "hint": "chrome://extensions → House Victoria Browser Capture → Reload",
        }
    if not result.get("ok"):
        return {
            "ok": False,
            "error": result.get("error") or "action_failed",
            "detail": result.get("detail"),
            "url": result.get("url"),
            "title": result.get("title"),
            "tab_id": result.get("tab_id"),
        }
    return {
        "ok": True,
        "detail": result.get("detail") or "ok",
        "url": result.get("url"),
        "title": result.get("title"),
        "tab_id": result.get("tab_id"),
        "hint": "Call browser_capture_tab again to verify the page after this action.",
    }


def _finalize_capture_result(result: dict[str, Any]) -> dict[str, Any]:
    if not result.get("ok"):
        return result

    screenshot_path: str | None = None
    b64 = result.get("screenshot_base64")
    if b64:
        try:
            raw = base64.b64decode(b64)
            out_dir = Path.home() / ".house_victoria" / "browser_captures"
            out_dir.mkdir(parents=True, exist_ok=True)
            fname = f"tab-{int(time.time())}.png"
            path = out_dir / fname
            path.write_bytes(raw)
            screenshot_path = str(path)
        except Exception as ex:
            result["screenshot_save_error"] = str(ex)

    page_map = result.get("page_map") or {}
    return {
        "ok": True,
        "url": result.get("url") or page_map.get("url"),
        "title": result.get("title") or page_map.get("title"),
        "tab_id": result.get("tab_id"),
        "screenshot_path": screenshot_path,
        "screenshot_base64_length": len(b64) if b64 else 0,
        "page_map": page_map,
        "hint": "Use page_map.elements selector/index with browser_click / browser_type. "
        "Fall back to x/y or browser_key when needed. Prefer these over computer_use for browser tabs.",
    }


def main() -> None:
    uvicorn.run(app, host="127.0.0.1", port=DEFAULT_PORT, log_level="info")


if __name__ == "__main__":
    main()
