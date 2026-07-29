using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary><c>desktop_screenshot</c> — gated by <see cref="ToolsOptions.AllowDesktopCapture"/>.</summary>
public sealed class DesktopScreenshotTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"monitor":{"type":"integer","description":"Monitor index: 0=virtual/primary screen, 1..N=specific display (Windows).","default":0}}}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IDesktopControlBackend _backend;

    public DesktopScreenshotTool(IOptions<ToolsOptions> options, IDesktopControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_screenshot",
        Description: "Capture the desktop screen.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!DesktopToolGate.IsCaptureAllowed(_options.Value))
            return new ToolResult(false, DesktopToolGate.CaptureDeniedMessage, null);

        var monitor = 0;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("monitor", out var m)
            && m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var mi))
        {
            monitor = mi;
        }

        var result = await _backend.ScreenshotAsync(monitor, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
