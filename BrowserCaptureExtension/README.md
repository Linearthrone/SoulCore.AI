# House Victoria Browser Capture

Captures and **drives** the **active browser tab** for House Victoria — bypassing desktop framebuffer issues caused by the Topmost overlay.

## Why

Desktop screenshot tools capture the composited desktop. House Victoria runs as a full-screen **Topmost** overlay, so screenshots often show HV chrome instead of the browser underneath.

This extension:

1. Captures **inside the browser** via `chrome.tabs.captureVisibleTab` + DOM page map
2. Drives the **same tab** with click/type/key/scroll (DOM or Chrome debugger CDP) — no OS mouse

## Desktop live preview (Instrument Stack → Desktop tab)

When you open the **Desktop** tab in House Victoria:

1. HV enables cast and connects as **consumer** on `ws://127.0.0.1:17891/ws/cast`
2. The extension connects as **producer** and pushes tab frames over the socket (~750ms)
3. Frames appear instantly in the live preview (no HTTP polling, no overlay capture)

HTTP `/latest.png` and `/capture` remain as fallbacks.

## Architecture

```
Chrome extension ──WebSocket──► Bridge :17891/ws/cast ──WebSocket──► House Victoria
       │                              │
       └──── HTTP poll (jobs) ───────┘  /capture  +  /action
```

SoulCore Host talks to the bridge directly (`Tools:BrowserBackend=bridge`) — no third-party agent gateway required.

## Install (one-time)

### 1. Start the bridge

```powershell
cd C:\Users\kurtw\LLMOD\LLMOD-max-master
.\Tools\install-browser-capture.ps1
```

Or manually:

```powershell
# From Soul_Core repo:
python BrowserCaptureBridge\bridge_server.py
# or LLMOD venv:
MCPServer\.venv\Scripts\python.exe BrowserCaptureBridge\bridge_server.py
```

Verify: `Invoke-WebRequest http://127.0.0.1:17891/health -UseBasicParsing`

### 2. Load / reload the extension

**Chrome:** `chrome://extensions` → Developer mode → **Load unpacked** → select `BrowserCaptureExtension`

**Edge:** `edge://extensions` → Developer mode → **Load unpacked** → same folder

After updating to **1.3.0+**, click **Reload** on the extension card (adds `debugger` permission for coordinate/key actions).

Click the extension icon — popup should show **bridge connected :17891**.

### 3. Restart SoulCore Host / House Victoria

Host must have `Tools:BrowserBackend=bridge` (default). Recycle Host after config changes.

## SoulCore tools

| Tool | Purpose |
|------|---------|
| `browser_capture_tab` | Screenshot + page map |
| `browser_health` | Bridge status |
| `browser_click` | Click by selector / index / x,y |
| `browser_type` | Type into element or focused field |
| `browser_key` | Key / combo (Enter, Ctrl+A, …) |
| `browser_scroll` | Scroll by delta or to element |

These work whenever the bridge + extension are healthy — **not** gated on AllowComputerControl.

## Debugger banner

Element actions (selector/index click & type) use DOM APIs and do **not** show a banner.

Coordinate clicks (`x`/`y`) and `browser_key` use `chrome.debugger` — Chrome may show:

> House Victoria Browser Capture started debugging this browser

That is expected. The extension detaches after ~60s idle.

## Capture output

```json
{
  "ok": true,
  "url": "https://example.com",
  "title": "Example",
  "screenshot_path": "C:\\Users\\...\\.house_victoria\\browser_captures\\tab-123.png",
  "page_map": {
    "elements": [
      { "index": 4, "tag": "button", "text": "Submit", "center": { "x": 120, "y": 340 }, "selector": "#submit" }
    ]
  }
}
```

Interact with `browser_click(selector="#submit")` or `browser_click(index=4)` — prefer these over desktop capture for browser tabs.

## Routing (automatic)

When the user asks about a **browser tab / webpage**, Victoria is steered to `browser_capture_tab` and the drive tools above.

Desktop-wide (non-browser) requests still use desktop control when AllowComputerControl is on.
