using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes MCP <c>computer_use</c> desktop backend stub (BED-135).
/// Full MCP tool-forced routing lands in BED-144; this ticket wires the
/// seam so <c>DesktopBackend=hermes</c> resolves through <see cref="IHermesClient"/>.
/// </summary>
/// <remarks>
/// Request flow (target, BED-144):
/// <c>desktop_*</c> tool → this backend → Hermes <c>/v1/chat/completions</c>
/// with <c>tool_choice</c> forcing MCP <c>computer_use</c> /
/// <c>list_desktop_windows</c> / <c>focus_desktop_window</c> → translate
/// OpenAI tool result → <see cref="DesktopOpResult"/>.
/// Today: health probe via <see cref="IHermesClient.GetHealthAsync"/>; if the
/// gateway is down return <c>hermes gateway unavailable</c>; if up, return a
/// pending-BED-144 marker so callers know the seam is live without pretending
/// MCP dispatch works yet.
/// </remarks>
public sealed class HermesDesktopControlBackend : IDesktopControlBackend
{
    /// <summary>
    /// Content when Hermes is reachable but MCP computer_use routing is not
    /// polished yet (BED-144 owns that).
    /// </summary>
    public const string PendingBed144Marker =
        "hermes computer_use routing pending BED-144 (gateway reachable; MCP-direct dispatch not wired)";

    public const string GatewayUnavailable = "hermes gateway unavailable";

    private readonly IHermesClient _hermes;

    public HermesDesktopControlBackend(IHermesClient hermes)
    {
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
    }

    public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
        => ProbeAsync($"screenshot(monitor={monitor})", ct);

    public Task<DesktopOpResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
        => ProbeAsync($"click({x},{y},{button})", ct);

    public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default)
        => ProbeAsync($"type(len={text?.Length ?? 0})", ct);

    public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default)
        => ProbeAsync($"key({key})", ct);

    public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default)
        => ProbeAsync("list_windows", ct);

    public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default)
        => ProbeAsync($"focus_window({title})", ct);

    private async Task<DesktopOpResult> ProbeAsync(string op, CancellationToken ct)
    {
        string health;
        try
        {
            health = await _hermes.GetHealthAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DesktopOpResult(
                false,
                $"{GatewayUnavailable}: {ex.GetType().Name}: {ex.Message}",
                new { op, backend = "hermes" });
        }

        if (string.IsNullOrWhiteSpace(health))
        {
            return new DesktopOpResult(
                false,
                GatewayUnavailable,
                new { op, backend = "hermes" });
        }

        // Seam is live (IHermesClient present + gateway answered). BED-144
        // replaces this with MCP tool_choice-forced computer_use dispatch.
        return new DesktopOpResult(
            false,
            PendingBed144Marker,
            new { op, backend = "hermes", healthPreview = Truncate(health, 120) });
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
