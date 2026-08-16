# House Victoria Browser Capture

Captures and **drives** the **active browser tab** for SoulCore / House Victoria —
screenshot + page map + click/type/key/scroll (DOM or Chrome debugger CDP).

## Install (one-time)

### 1. Start the bridge

```powershell
cd C:\Users\kurtw\Soul_Core
.\SoulCore\scripts\start-browser-bridge.ps1
```

Or let `.\ALLSTART.ps1` start it (soft-fail if Python/deps missing).

Verify: `Invoke-RestMethod http://127.0.0.1:17891/health`

### 2. Load / reload the extension

**Chrome:** `chrome://extensions` → Developer mode → **Load unpacked** → select

`C:\Users\kurtw\Soul_Core\BrowserCaptureExtension`

**Edge:** `edge://extensions` → same folder.

After updates, click **Reload** on the extension card.

Click the extension icon — popup should show **bridge connected :17891**.

### 3. SoulCore Host

`Tools:BrowserBackend=native` (default) routes `browser_*` tools to this bridge.
No Hermes required.

## SoulCore tools

| Tool | Bridge API | Purpose |
|------|------------|---------|
| `browser_health` | `GET /health` | Bridge status |
| `browser_capture_tab` | `POST /capture` | Screenshot + page map |
| `browser_click` | `POST /action` click | Click by x,y (or selector via page_map) |
| `browser_type` | `POST /action` type | Type into focused field |
| `browser_key` | `POST /action` key | Key / combo |
| `browser_scroll` | `POST /action` scroll | Scroll deltas |

## Debugger banner

Coordinate clicks and `browser_key` may show Chrome’s debugger banner — expected.
Element selector/index actions use DOM APIs without the banner.
