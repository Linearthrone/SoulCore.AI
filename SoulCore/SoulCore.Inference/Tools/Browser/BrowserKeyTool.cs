using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary><c>browser_key</c> — gated by <see cref="ToolsOptions.AllowComputerControl"/>.</summary>
public sealed class BrowserKeyTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"key":{"type":"string","description":"Key name (e.g. Enter, Escape, Tab)."}},"required":["key"]}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IBrowserControlBackend _backend;

    public BrowserKeyTool(IOptions<ToolsOptions> options, IBrowserControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_key",
        Description: "Press a key in the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options.Value))
            return new ToolResult(false, BrowserToolGate.ControlDeniedMessage, null);

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("key", out var keyProp)
            || keyProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "browser_key requires 'key' (string)", null);
        }

        var key = keyProp.GetString() ?? string.Empty;
        var result = await _backend.KeyAsync(key, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
