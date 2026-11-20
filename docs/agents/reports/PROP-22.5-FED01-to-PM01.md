# PROP-22.5 — FED-01 → PM-01: status orb + radial popup

**Status:** Pass (build + unit tests + Windows smoke).

## Implementation

- `Services/SystemStatusAggregator.cs` (pure, unit-tested): eight lights — Host, Companion, Inference, Unreal, ComfyUI, VM, Voice, Mailbox. Priority: **1** Host down → fast red flash; **2** WS or active-inference down → solid red; **3** pink pulse (`currentActivity` non-empty OR call OR typing); **4** amber (≥2 non-critical down); **5** yellow (1 down); **6** green. *Intentionally off* (Unreal disabled, ComfyUI silent, VM stopped, voice disabled, no mailbox, inference disabled in Host) is a dim light and is **never** counted as down. Host down does not paint the other seven as fake downs.
- `MainWindow.GlassShell.cs`: `BuildStatusFacts` (from `_lastHealth`, `_ws.State`, Ollama/Comfy/VM probes, voice health, mailbox accounts, `_presenceCurrentActivity`, `_callActive`, `_typingActive`) → `RefreshStatusOrb`. Wired from `ProbeHealthAsync` / `UpdateConnectionChrome` / `RefreshServicesPanelAsync` / `RefreshVoiceStatusAsync` / `SetTyping` / call phases. A 60 ms `DispatcherTimer` runs **only** while the state is animated (fast flash ≈3 Hz; pink = 2.4 s breathing opacity). Until the first `/health` answer the orb stays dim (no false red flash on startup).
- `Controls/RadialStatusWindow` — transparent borderless window placed **outside the upper-left** of the main window (left, else above); orb click toggles; closes on deactivate (click outside) or Esc; `ClosedRecently` guard stops the deactivating click from re-opening it. Implemented as a glass **list** with a coloured light per service + one-line detail (the brief allowed wedges/list). Live-updates while open.

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`SystemStatusAggregatorTests` — 16 cases (priority order, off-never-red, Host-down-flash beats busy, WS red beats pink, busy hides yellow/amber but never red, empty/whitespace activity not busy, stable eight-service order). All pass (see 22.4 test summary).

Windows smoke against the live Host: orb pulsed pink while the Host reported a real activity ("Scrolling"/"Clicking"); clicking it opened the popup outside the window's upper-left showing Busy · Host (answering /health) · Companion (WS connected) · Inference (deepseek… cloud) · Unreal (enabled, not connected → yellow) · ComfyUI (off, dim) · VM (KsDevice running) · Voice (STT ok · TTS ok) · Mailbox (no mailbox, dim).

## Notes

- The wedge/ring variant from the mockups is not implemented; the list satisfies "outside radial popup" semantically and is easier to keep readable.
