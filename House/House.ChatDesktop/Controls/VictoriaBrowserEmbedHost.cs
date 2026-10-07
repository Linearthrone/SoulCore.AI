using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
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
            // CHILD style so layout lives inside Presence; keep visible.
            var originalStyle = GetWindowLong(_hwnd, GWL_STYLE);
            var childStyle = (originalStyle | WS_CHILD | WS_VISIBLE) & ~WS_POPUP;
            _ = SetWindowLong(_hwnd, GWL_STYLE, childStyle);

            Marshal.SetLastPInvokeError(0);
            if (SetParent(_hwnd, parent.Handle) == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                _ = SetWindowLong(_hwnd, GWL_STYLE, originalStyle);
                return CreateFallbackHost(parent);
            }

            _attached = true;
            _placeholderHandle = null;
            ResizeToHost(parent.Handle);
            _ = ShowWindow(_hwnd, SW_SHOW);
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
            ResizeToHost(nint.Zero);
    }

    private void ResizeToHost(nint parentHint)
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
        _ = MoveWindow(_hwnd, 0, 0, width, height, true);
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
            var style = GetWindowLong(_hwnd, GWL_STYLE);
            style = (style | WS_POPUP | WS_VISIBLE) & ~WS_CHILD;
            _ = SetWindowLong(_hwnd, GWL_STYLE, style);
            _ = ShowWindow(_hwnd, SW_SHOW);
        }
        catch
        {
            // Best-effort detach — Chromium may already be gone.
        }

        _attached = false;
        _previousParent = 0;
    }

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int SW_SHOW = 5;

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
    private static extern bool MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
