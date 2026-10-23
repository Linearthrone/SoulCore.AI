using Avalonia.Controls;

namespace House.ChatDesktop.Services;

public enum ViewportDialogKind
{
    ToolConfirm,
    QuarantineConfirm,
    UpdateApply
}

/// <summary>
/// Clickthrough, view opacity, and consent pause for the viewport drawer only.
/// View opacity never writes chrome opacity. Clickthrough starts off.
/// An open tool, quarantine, or update-apply dialog forces clickthrough off and pauses CUA.
/// </summary>
public sealed class ViewportInputPolicy
{
    public const double ViewOpacityFloor = 0.15;
    public const double DefaultViewOpacity = 1d;

    private readonly List<ViewportDialogKind> _open = new();

    public ViewportInputPolicy()
    {
        ViewOpacity = DefaultViewOpacity;
        ChromeOpacity = GlassChrome.DefaultOpacity;
    }

    public event Action? Changed;

    public bool RequestedClickthrough { get; private set; }

    public bool EffectiveClickthrough => RequestedClickthrough && _open.Count == 0;

    public bool CuaPaused => _open.Count > 0;

    public double ViewOpacity { get; private set; }

    public double ChromeOpacity { get; private set; }

    public IReadOnlyList<ViewportDialogKind> OpenDialogs => _open;

    public void SetClickthrough(bool on)
    {
        if (_open.Count > 0)
            on = false;
        if (RequestedClickthrough == on)
            return;
        RequestedClickthrough = on;
        Changed?.Invoke();
    }

    public void SetViewOpacity(double value)
    {
        var next = ClampViewOpacity(value);
        if (Math.Abs(ViewOpacity - next) < 0.0001)
            return;
        ViewOpacity = next;
        Changed?.Invoke();
    }

    public void SetChromeOpacity(double value)
    {
        var next = GlassChrome.ClampOpacity(value);
        if (Math.Abs(ChromeOpacity - next) < 0.0001)
            return;
        ChromeOpacity = next;
        Changed?.Invoke();
    }

    public IDisposable Open(ViewportDialogKind kind)
    {
        _open.Add(kind);
        RequestedClickthrough = false;
        Changed?.Invoke();
        return new DialogScope(this, kind);
    }

    public static double ClampViewOpacity(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            return DefaultViewOpacity;
        if (value < ViewOpacityFloor)
            return ViewOpacityFloor;
        if (value > 1d)
            return 1d;
        return value;
    }

    /// <summary>
    /// Null means the guest stays fully opaque and is not given a layered style.
    /// Chrome opacity is not an input.
    /// </summary>
    public static byte? GuestLayerAlpha(double viewOpacity)
    {
        var clamped = ClampViewOpacity(viewOpacity);
        if (clamped >= 0.999d)
            return null;
        return (byte)Math.Round(clamped * 255d);
    }

    public static string Label(ViewportDialogKind kind) => kind switch
    {
        ViewportDialogKind.ToolConfirm => "tool confirm",
        ViewportDialogKind.QuarantineConfirm => "quarantine confirm",
        ViewportDialogKind.UpdateApply => "update apply",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private void Close(ViewportDialogKind kind)
    {
        var index = _open.LastIndexOf(kind);
        if (index < 0)
            return;
        _open.RemoveAt(index);
        Changed?.Invoke();
    }

    private sealed class DialogScope : IDisposable
    {
        private readonly ViewportInputPolicy _policy;
        private readonly ViewportDialogKind _kind;
        private bool _disposed;

        public DialogScope(ViewportInputPolicy policy, ViewportDialogKind kind)
        {
            _policy = policy;
            _kind = kind;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _policy.Close(_kind);
        }
    }
}

/// <summary>Top-left quarter of the work area. The top edge sits inside that quarter.</summary>
public static class ViewportLayout
{
    public const double TopEdgeDip = 36;

    public readonly record struct Quarter(int X, int Y, int WidthPx, int HeightPx);

    public static Quarter OfWorkArea(int x, int y, int width, int height) =>
        new(x, y, Math.Max(1, width / 2), Math.Max(1, height / 2));
}

/// <summary>
/// Clickthrough style bits for the viewport guest window only.
/// Enabling clickthrough detaches the guest first. Parenting is refused while the bits are set.
/// </summary>
public static class ViewportClickThrough
{
    public const int ExStyleIndex = -20;
    public const int Layered = 0x00080000;
    public const int Transparent = 0x00000020;
    public const int AlphaFlag = 0x2;
    public const string Scope = "viewport-guest";

    public readonly record struct Plan(bool DetachGuestFirst, int ExStyleAfter, bool ParentAllowed);

    public static int Apply(int exStyle, bool clickthrough) =>
        clickthrough
            ? exStyle | Layered | Transparent
            : exStyle & ~(Layered | Transparent);

    public static bool AllowsParent(int exStyle) =>
        (exStyle & (Layered | Transparent)) == 0;

    public static Plan Describe(int exStyle, bool clickthrough)
    {
        var next = Apply(exStyle, clickthrough);
        return new Plan(clickthrough, next, AllowsParent(next));
    }
}

/// <summary>
/// The viewport window may host a guest only when it is neither frosted nor layered-transparent.
/// </summary>
public static class ViewportMaterial
{
    public static bool AllowsGuest(IReadOnlyList<WindowTransparencyLevel> hints)
    {
        if (hints.Count == 0)
            return true;
        foreach (var level in hints)
        {
            if (level != WindowTransparencyLevel.None)
                return false;
        }

        return true;
    }
}
