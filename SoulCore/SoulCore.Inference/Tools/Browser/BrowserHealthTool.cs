using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary><c>browser_health</c> — gated by <see cref="ToolsOptions.AllowBrowserCapture"/>.</summary>
public sealed class BrowserHealthTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{}}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IBrowserControlBackend _backend;

    public BrowserHealthTool(IOptions<ToolsOptions> options, IBrowserControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_health",
        Description: "Check browser bridge status.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsCaptureAllowed(_options.Value))
            return new ToolResult(false, BrowserToolGate.CaptureDeniedMessage, null);

        var result = await _backend.HealthAsync(ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
