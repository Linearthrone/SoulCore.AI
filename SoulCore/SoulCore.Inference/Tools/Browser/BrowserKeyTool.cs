using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_key</c> — press a key in the browser tab.
/// Write/control; gated by <see cref="ToolsOptions.AllowComputerControl"/>.
/// </summary>
public sealed class BrowserKeyTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"key":{"type":"string","description":"Key to press (e.g. Enter, Escape, Tab)."}},"required":["key"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly ToolsOptions _options;

    public BrowserKeyTool(IBrowserBridge bridge, IOptions<ToolsOptions> options)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_key",
        Description: "Press a key in the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);

        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: browser_key expects a JSON object with 'key'.", null);

        if (!args.TryGetProperty("key", out var keyProp) || keyProp.ValueKind != JsonValueKind.String)
            return new ToolResult(false, "error: browser_key requires 'key' (string).", null);

        var key = keyProp.GetString();
        if (string.IsNullOrWhiteSpace(key))
            return new ToolResult(false, "error: browser_key 'key' must be non-empty.", null);

        var result = await _bridge.KeyAsync(key.Trim(), ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
