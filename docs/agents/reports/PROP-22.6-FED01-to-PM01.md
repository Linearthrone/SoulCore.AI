# PROP-22.6 — FED-01 → PM-01: VM drawer popup (left/right) + HWND embed

**Status:** Pass (build + Windows smoke of open/embed/close). 

## Implementation

- `Controls/VmDrawerWindow` — opaque borderless popup (560 wide) docked beside the main slab (`DockTo` is pixel-accurate, clamps to the working area, matches height). It holds *Her screen* (`VictoriaBrowserEmbedSlot`), the simplified *What she saw* panel, and `VmPowerButton`. The `NativeControlHost` is **not** in any XAML and **never** inside a TabControl/template: `VictoriaBrowserEmbedHost` is added to the slot by the existing bind code, so it has a real layout rectangle.
- `MainWindow` keeps `private readonly VmDrawerWindow _vmDrawer = new();` (constructed, never shown → no native cost) and expression-bodied shim properties for every control the BrowserView/Desktop partials reference, so those files compile unchanged. Handlers that were XAML attributes are wired in `InitGlassShell`.
- `VmDrawerTab` on the left/right edge toggles the popup. **Open**: dock → `Show(this)` → `ApplyVmPowerButton` → `ShowPresenceSide(herScreen:true)`. **Close/✕/Esc/Alt+F4**: `ClearVictoriaBrowserEmbed(null)` (existing pattern: unbind HWND, destroy host, hide overlays) then `Hide()`; `Closing` is cancelled into a hide so the window can reopen. BrowserView polling is skipped while the drawer is closed (guard changed to `_vmDrawer is not { IsVisible: true }`).
- Side persisted as `LocalUiSettings.VmDrawerSide` (`left|right`, default right); Settings has a *VM drawer side* combo; the drawer follows window move/resize via `RepositionVmDrawer`.
- Removed: pop-out windows, AlwaysOnTop checkboxes, east resize grip, activity band under the VM, side column.

## Evidence

```
> dotnet build House/House.ChatDesktop/House.ChatDesktop.csproj --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Windows smoke: tab opened the drawer docked to the right, full height, with the live KsDevice desktop embedded (Ubuntu guest visible); ✕ closed it and the embed detached cleanly (no crash, no orphan child window).

## Caveats

- Opening the drawer in a second Presence instance **steals** the VirtualBox window from the first (single HWND parent). I did this once during smoke against a stale Presence; closing the drawer returned the VM to Host-side polling.
- Not exercised: the HWND sibling overlay for soft-cursor/click input while the drawer is open, and the drawer on the **left**. Needs a Windows human pass.
