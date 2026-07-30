using System.Text.Json;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// <c>browser_type</c> — type text into the browser tab.
/// Write/control; gated by computer-control session opt-in.
/// </summary>
public sealed class BrowserTypeTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"text":{"type":"string","description":"Text to type."}},"required":["text"]}""")
        .RootElement.Clone();

    private readonly IBrowserBridge _bridge;
    private readonly IToolsAccessSettings _access;

    public BrowserTypeTool(IBrowserBridge bridge, IToolsAccessSettings access)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_type",
        Description: "Type into the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_access))
            return new ToolResult(false, BrowserToolGate.ControlDenied, null);

        if (args.ValueKind != JsonValueKind.Object)
            return new ToolResult(false, "error: browser_type expects a JSON object with 'text'.", null);

        if (!args.TryGetProperty("text", out var textProp) || textProp.ValueKind != JsonValueKind.String)
            return new ToolResult(false, "error: browser_type requires 'text' (string).", null);

        var text = textProp.GetString();
        if (text is null)
            return new ToolResult(false, "error: browser_type requires 'text' (string).", null);

        var result = await _bridge.TypeAsync(text, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Content, result.Data);
    }
}
