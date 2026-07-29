using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary><c>browser_scroll</c> — gated by <see cref="ToolsOptions.AllowComputerControl"/>.</summary>
public sealed class BrowserScrollTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"dx":{"type":"integer","description":"Horizontal scroll delta in pixels.","default":0},"dy":{"type":"integer","description":"Vertical scroll delta in pixels.","default":0}}}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IBrowserControlBackend _backend;

    public BrowserScrollTool(IOptions<ToolsOptions> options, IBrowserControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_scroll",
        Description: "Scroll the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options.Value))
            return new ToolResult(false, BrowserToolGate.ControlDeniedMessage, null);

        var dx = 0;
        var dy = 0;
        if (args.ValueKind == JsonValueKind.Object)
        {
            if (args.TryGetProperty("dx", out var dxProp) && dxProp.ValueKind == JsonValueKind.Number
                && dxProp.TryGetInt32(out var dxi))
            {
                dx = dxi;
            }

            if (args.TryGetProperty("dy", out var dyProp) && dyProp.ValueKind == JsonValueKind.Number
                && dyProp.TryGetInt32(out var dyi))
            {
                dy = dyi;
            }
        }

        var result = await _backend.ScrollAsync(dx, dy, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
