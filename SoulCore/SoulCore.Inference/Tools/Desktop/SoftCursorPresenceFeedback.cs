namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Presence soft-cursor timing for Her screen (VM embed / guest JPEG).
/// Matches Playwright aim dwell so Kayleigh sees pink move then teal click.
/// </summary>
public static class SoftCursorPresenceFeedback
{
    /// <summary>Brief pause after publishing idle aim so Presence (~5fps) can paint pink.</summary>
    public const int DefaultAimLeadMs = 150;

    /// <summary>Hold after click — same spirit as <c>PlaywrightBrowserBridge.AimDwellMs</c>.</summary>
    public const int DefaultClickDwellMs = 650;

    /// <summary>Test hook — set to 0 to skip dwell; null restores defaults.</summary>
    public static int? AimLeadMsForTests { get; set; }

    /// <summary>Test hook — set to 0 to skip dwell; null restores defaults.</summary>
    public static int? ClickDwellMsForTests { get; set; }

    public static int AimLeadMs => AimLeadMsForTests ?? DefaultAimLeadMs;
    public static int ClickDwellMs => ClickDwellMsForTests ?? DefaultClickDwellMs;

    public static async Task AnnounceAimAsync(
        IDesktopViewHub? view,
        int x,
        int y,
        CancellationToken ct)
    {
        if (view is null)
            return;
        view.RecordAction($"agent cursor → ({x},{y})", x, y);
        await Task.Delay(AimLeadMs, ct).ConfigureAwait(false);
    }

    public static async Task AnnounceClickAsync(
        IDesktopViewHub? view,
        string action,
        int x,
        int y,
        CancellationToken ct)
    {
        if (view is null)
            return;
        var note = string.IsNullOrWhiteSpace(action)
            ? $"clicked at ({x},{y})"
            : action.Trim();
        view.RecordAction(note, x, y);
        await Task.Delay(ClickDwellMs, ct).ConfigureAwait(false);
    }
}
