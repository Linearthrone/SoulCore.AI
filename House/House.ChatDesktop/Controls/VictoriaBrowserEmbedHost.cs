using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Controls;

/// <summary>
/// PROP-14.2 / VM embed: hosts an existing Win32 HWND (VirtualBox victoria-sandbox or
/// Playwright Chromium) inside Presence Her screen via SetParent.
/// Detach restores the prior parent — never DestroyWindow the guest/VM HWND.
/// Non-Windows / zero hwnd → no native child (JPEG fallback stays visible).
/// Requires Windows app.manifest with supportedOS (see House.ChatDesktop/app.manifest).
/// Avalonia NativeControlHost creates a Win32 child on visual-tree attach even when
/// IsVisible=False — never put this control in the tree until Bind has a real HWND.
/// </summary>
public sealed class VictoriaBrowserEmbedHost : NativeControlHost
{
    private nint _hwnd;
    private nint _previousParent;
    private int _originalStyle;
    private bool _attached;
    private bool _nativeHostUnavailable;
    private IPlatformHandle? _placeholderHandle;

    public nint Hwnd
    {
        get => _hwnd;
        set => Bind(value);
    }

    /// <summary>True when Win32 child-host creation failed (missing manifest / OS). JPEG fallback only.</summary>
    public bool NativeHostUnavailable => _nativeHostUnavailable;

    /// <summary>Fired after the guest HWND is resized — Presence raises the soft-cursor sibling.</summary>
    public event EventHandler? SoftCursorSiblingRaised;

    /// <summary>
    /// Parent HWND of the SetParent'd guest (Avalonia NativeControlHost). Soft-cursor
    /// overlay attaches as a sibling of the VM so it paints above the GPU surface.
    /// </summary>
    public nint EmbedParentHwnd
    {
        get
        {
            if (!OperatingSystem.IsWindows() || !_attached || _hwnd == 0)
                return 0;
            return GetParent(_hwnd);
        }
    }

    /// <summary>
    /// Set the Chromium HWND before attaching to the visual tree.
    /// Attach only after Bind with a non-zero hwnd so CreateNativeControlCore sees it.
    /// </summary>
    public void Bind(nint hwnd)
    {
        if (_nativeHostUnavailable)
        {
            IsVisible = false;
            return;
        }

        if (_hwnd == hwnd && hwnd == 0)
        {
            IsVisible = false;
            return;
        }

        if (_hwnd == hwnd && _attached)
            return;

        DetachIfNeeded();
        _hwnd = hwnd;

        if (!OperatingSystem.IsWindows() || _hwnd == 0)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
    }

    /// <summary>
    /// Force the embedded HWND to match this control's current slot (DIP × DPI).
    /// Call after pane/splitter resize — VirtualBox otherwise keeps its old outer size and clips.
    /// </summary>
    public void SyncSizeToSlot()
    {
        if (!_attached || _hwnd == 0 || !OperatingSystem.IsWindows())
            return;

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var w = (int)Math.Round(Bounds.Width * scaling);
        var h = (int)Math.Round(Bounds.Height * scaling);
        if (w > 1 && h > 1)
        {
            ApplyPixelSize(w, h);
            // Keep soft-cursor sibling above VirtualBox after every resize.
            SoftCursorSiblingRaised?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Bounds not measured yet — fall back to parent client rect.
        ResizeToParentClient(nint.Zero);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_nativeHostUnavailable)
            return;

        try
        {
            base.OnAttachedToVisualTree(e);
        }
        catch (Exception ex)
        {
            // Avalonia Win32NativeControlHost.DumbWindow without supportedOS manifest
            // throws InvalidOperationException here — never take down Presence.
            MarkUnavailable("VictoriaBrowserEmbedHost.OnAttachedToVisualTree", ex);
            try
            {
                // Best-effort cleanup if base partially registered visual-tree hooks.
                base.OnDetachedFromVisualTree(e);
            }
            catch
            {
                // ignore
            }
        }
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (_nativeHostUnavailable || !OperatingSystem.IsWindows() || parent.Handle == 0)
            return CreateFallbackHost(parent);

        if (_hwnd == 0)
            return CreateFallbackHost(parent);

        try
        {
            _previousParent = GetParent(_hwnd);
            // CHILD + strip overlapped chrome so MoveWindow can shrink below guest+chrome
            // minimums (otherwise the right side of the VM stays clipped in the slot).
            _originalStyle = GetWindowLong(_hwnd, GWL_STYLE);
            var childStyle = (_originalStyle | WS_CHILD | WS_VISIBLE)
                             & ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX
                                 | WS_MAXIMIZEBOX | WS_SYSMENU | WS_BORDER | WS_DLGFRAME);
            _ = SetWindowLong(_hwnd, GWL_STYLE, childStyle);

            Marshal.SetLastPInvokeError(0);
            if (SetParent(_hwnd, parent.Handle) == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                _ = SetWindowLong(_hwnd, GWL_STYLE, _originalStyle);
                _originalStyle = 0;
                return CreateFallbackHost(parent);
            }

            _attached = true;
            _placeholderHandle = null;
            ResizeToParentClient(parent.Handle);
            _ = ShowWindow(_hwnd, SW_SHOW);
            // First layout pass often has 0×0 bounds — sync again after measure.
            Dispatcher.UIThread.Post(SyncSizeToSlot, DispatcherPriority.Loaded);
            return new PlatformHandle(_hwnd, "HWND");
        }
        catch (Exception ex)
        {
            // Never take down Presence for a bad Chromium HWND — JPEG fallback stays.
            PresenceStartupLog.WriteException("VictoriaBrowserEmbedHost.CreateNativeControlCore", ex);
            _attached = false;
            return CreateFallbackHost(parent);
        }
    }

    /// <summary>
    /// Empty WS_CHILD placeholder (or safe base host). Never rethrow CreateWindowEx failures
    /// into MainWindow construction — mark unavailable and stay on JPEG.
    /// </summary>
    private IPlatformHandle CreateFallbackHost(IPlatformHandle parent)
    {
        try
        {
            var handle = base.CreateNativeControlCore(parent);
            _placeholderHandle = handle;
            return handle;
        }
        catch (Exception ex)
        {
            MarkUnavailable("VictoriaBrowserEmbedHost.CreateFallbackHost", ex);
            // Last resort: return parent so Avalonia has a handle; never DestroyWindow it.
            _placeholderHandle = null;
            return parent;
        }
    }

    private void MarkUnavailable(string stage, Exception ex)
    {
        _nativeHostUnavailable = true;
        _attached = false;
        IsVisible = false;
        PresenceStartupLog.WriteException(stage, ex);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        DetachIfNeeded();
        // Do not DestroyWindow Chromium, and do not DestroyWindow an Avalonia parent
        // HWND we may have returned as a last-resort fallback handle.
        if (_placeholderHandle is not null
            && control.Handle != 0
            && control.Handle == _placeholderHandle.Handle
            && control.Handle != _hwnd)
        {
            try
            {
                base.DestroyNativeControlCore(control);
            }
            catch
            {
                // ignore
            }

            _placeholderHandle = null;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_attached && _hwnd != 0 && OperatingSystem.IsWindows())
            SyncSizeToSlot();
    }

    private void ResizeToParentClient(nint parentHint)
    {
        if (!OperatingSystem.IsWindows() || _hwnd == 0)
            return;
        var parent = parentHint != 0 ? parentHint : GetParent(_hwnd);
        if (parent == 0)
            return;
        if (!GetClientRect(parent, out var rc))
            return;
        var width = rc.Right - rc.Left;
        var height = rc.Bottom - rc.Top;
        // Layout may not be measured on first attach — never shrink the VM to 0x0
        // (that looked like "window pops up and immediately disappears").
        if (width <= 1 || height <= 1)
            return;
        ApplyPixelSize(width, height);
    }

    private void ApplyPixelSize(int width, int height)
    {
        if (!OperatingSystem.IsWindows() || _hwnd == 0 || width <= 1 || height <= 1)
            return;

        // SetWindowPos is more reliable than MoveWindow for forcing VirtualBox below its
        // former overlapped minimum size after chrome styles are stripped.
        _ = SetWindowPos(
            _hwnd,
            HWND_TOP,
            0,
            0,
            width,
            height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
        _ = ShowWindow(_hwnd, SW_SHOW);
    }

    private void DetachIfNeeded()
    {
        if (!_attached || _hwnd == 0 || !OperatingSystem.IsWindows())
        {
            _attached = false;
            return;
        }

        try
        {
            _ = SetParent(_hwnd, _previousParent);
            if (_originalStyle != 0)
                _ = SetWindowLong(_hwnd, GWL_STYLE, _originalStyle);
            else
            {
                var style = GetWindowLong(_hwnd, GWL_STYLE);
                style = (style | WS_POPUP | WS_VISIBLE) & ~WS_CHILD;
                _ = SetWindowLong(_hwnd, GWL_STYLE, style);
            }

            _ = SetWindowPos(
                _hwnd,
                HWND_TOP,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            _ = ShowWindow(_hwnd, SW_SHOW);
        }
        catch
        {
            // Best-effort detach — Chromium may already be gone.
        }

        _attached = false;
        _previousParent = 0;
        _originalStyle = 0;
    }

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int SW_SHOW = 5;
    private static readonly nint HWND_TOP = nint.Zero;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetParent(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
