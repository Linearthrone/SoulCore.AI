using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary><c>focus_desktop_window</c> — gated by <see cref="ToolsOptions.AllowDesktopCapture"/>.</summary>
public sealed class FocusDesktopWindowTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"title":{"type":"string","description":"Window title substring to focus."}},"required":["title"]}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IDesktopControlBackend _backend;

    public FocusDesktopWindowTool(IOptions<ToolsOptions> options, IDesktopControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "focus_desktop_window",
        Description: "Focus a desktop window by title or index.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!DesktopToolGate.IsCaptureAllowed(_options.Value))
            return new ToolResult(false, DesktopToolGate.CaptureDeniedMessage, null);

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("title", out var titleProp)
            || titleProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "focus_desktop_window requires 'title' (string)", null);
        }

        var title = titleProp.GetString() ?? string.Empty;
        var result = await _backend.FocusWindowAsync(title, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
