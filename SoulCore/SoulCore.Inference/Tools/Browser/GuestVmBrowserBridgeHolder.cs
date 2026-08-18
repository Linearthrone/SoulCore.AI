using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// Holds the guest Firefox bridge once <see cref="ScopedDesktopControlBackend"/>
/// wires <see cref="VirtualBoxGuestAppLauncher"/> (VM scope).
/// </summary>
public sealed class GuestVmBrowserBridgeHolder
{
    private GuestVmBrowserBridge? _bridge;

    public void Set(IVmGuestDesktop desktop, IVmGuestBrowser browser) =>
        _bridge = new GuestVmBrowserBridge(desktop, browser);

    public bool TryGet(out IBrowserBridge bridge)
    {
        if (_bridge is null)
        {
            bridge = null!;
            return false;
        }

        bridge = _bridge;
        return true;
    }
}
