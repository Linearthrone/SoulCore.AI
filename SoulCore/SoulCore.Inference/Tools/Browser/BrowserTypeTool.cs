using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Browser;

/// <summary><c>browser_type</c> — gated by <see cref="ToolsOptions.AllowComputerControl"/>.</summary>
public sealed class BrowserTypeTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"text":{"type":"string","description":"Text to type into the focused element."}},"required":["text"]}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IBrowserControlBackend _backend;

    public BrowserTypeTool(IOptions<ToolsOptions> options, IBrowserControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "browser_type",
        Description: "Type into the browser tab.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!BrowserToolGate.IsControlAllowed(_options.Value))
            return new ToolResult(false, BrowserToolGate.ControlDeniedMessage, null);

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("text", out var textProp)
            || textProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "browser_type requires 'text' (string)", null);
        }

        var text = textProp.GetString() ?? string.Empty;
        var result = await _backend.TypeAsync(text, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
