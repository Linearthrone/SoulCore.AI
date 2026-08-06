using System.Text.Json;
using SoulCore.Inference;
using SoulCore.Inference.Tools;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Hermes;

/// <summary>
/// Hermes MCP desktop backend (BED-135 seam + BED-144 <see cref="IHermesMcpInvoker"/>).
/// BED-174: open_app / scroll / multi-click are best-effort MCP passthrough; when Hermes
/// lacks a matching tool, returns a clear Success:false directing native/cua.
/// </summary>
public sealed class HermesDesktopControlBackend : IDesktopControlBackend
{
    public const string GatewayUnavailable = IHermesMcpInvoker.UnavailableMessage;

    public const string OpenAppUseNativeMessage =
        "desktop_open_app is not available via Hermes DesktopBackend — set Tools:DesktopBackend=native or cua";

    private readonly IHermesClient _hermes;

    public HermesDesktopControlBackend(IHermesClient hermes)
    {
        _hermes = hermes ?? throw new ArgumentNullException(nameof(hermes));
    }

    public Task<DesktopOpResult> ScreenshotAsync(int monitor, CancellationToken ct = default) =>
        CallAsync("computer_use", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?> { ["action"] = "screenshot", ["monitor"] = monitor }), ct);

    public Task<DesktopOpResult> ClickAsync(
        int x, int y, string button, int clicks = 1, CancellationToken ct = default) =>
        CallAsync("computer_use", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?>
            {
                ["action"] = clicks >= 2 ? "double_click" : "click",
                ["x"] = x,
                ["y"] = y,
                ["button"] = button,
                ["clicks"] = clicks,
            }), ct);

    public Task<DesktopOpResult> DragAsync(
        int x1, int y1, int x2, int y2, string button, CancellationToken ct = default) =>
        CallAsync("computer_use", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?>
            {
                ["action"] = "drag",
                ["x1"] = x1,
                ["y1"] = y1,
                ["x2"] = x2,
                ["y2"] = y2,
                ["button"] = button,
            }), ct);

    public Task<DesktopOpResult> TypeAsync(string text, CancellationToken ct = default) =>
        CallAsync("computer_use", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?> { ["action"] = "type", ["text"] = text }), ct);

    public Task<DesktopOpResult> KeyAsync(string key, CancellationToken ct = default) =>
        CallAsync("computer_use", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?> { ["action"] = "key", ["key"] = key }), ct);

    public Task<DesktopOpResult> ScrollAsync(
        int x, int y, int deltaY, int deltaX = 0, CancellationToken ct = default) =>
        CallAsync("computer_use", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?>
            {
                ["action"] = "scroll",
                ["x"] = x,
                ["y"] = y,
                ["deltaY"] = deltaY,
                ["deltaX"] = deltaX,
            }), ct);

    public Task<DesktopOpResult> OpenAppAsync(
        string app, string? args = null, CancellationToken ct = default)
    {
        // Prefer native/cua for allowlisted Process.Start. Hermes has no stable
        // open_app MCP verb — return a clear directive (AC: no silent Hermes lure).
        _ = app;
        _ = args;
        _ = ct;
        return Task.FromResult(new DesktopOpResult(false, OpenAppUseNativeMessage, null));
    }

    public Task<DesktopOpResult> ListWindowsAsync(CancellationToken ct = default) =>
        CallAsync("list_desktop_windows", HermesToolRouting.EmptyArgs(), ct);

    public Task<DesktopOpResult> FocusWindowAsync(string title, CancellationToken ct = default) =>
        CallAsync("focus_desktop_window", HermesToolRouting.MergeObject(
            HermesToolRouting.EmptyArgs(),
            new Dictionary<string, object?> { ["title"] = title }), ct);

    private async Task<DesktopOpResult> CallAsync(string mcpTool, JsonElement args, CancellationToken ct)
    {
        var result = await _hermes.CallMcpToolAsync(mcpTool, args, ct).ConfigureAwait(false);
        return new DesktopOpResult(result.Success, result.Content, result.Data);
    }
}
