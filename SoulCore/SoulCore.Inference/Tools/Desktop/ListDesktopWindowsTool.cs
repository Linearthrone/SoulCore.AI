using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary><c>list_desktop_windows</c> — gated by <see cref="ToolsOptions.AllowDesktopCapture"/>.</summary>
public sealed class ListDesktopWindowsTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IDesktopControlBackend _backend;

    public ListDesktopWindowsTool(IOptions<ToolsOptions> options, IDesktopControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "list_desktop_windows",
        Description: "List open desktop windows.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!DesktopToolGate.IsCaptureAllowed(_options.Value))
            return new ToolResult(false, DesktopToolGate.CaptureDeniedMessage, null);

        var result = await _backend.ListWindowsAsync(ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
