using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary><c>desktop_type</c> — gated by <see cref="ToolsOptions.AllowComputerControl"/>.</summary>
public sealed class DesktopTypeTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"text":{"type":"string","description":"Text to type at the current focus."}},"required":["text"]}""")
        .RootElement.Clone();

    private readonly IOptions<ToolsOptions> _options;
    private readonly IDesktopControlBackend _backend;

    public DesktopTypeTool(IOptions<ToolsOptions> options, IDesktopControlBackend backend)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_type",
        Description: "Type text at the current focus.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!DesktopToolGate.IsControlAllowed(_options.Value))
            return new ToolResult(false, DesktopToolGate.ControlDeniedMessage, null);

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("text", out var textProp)
            || textProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "desktop_type requires 'text' (string)", null);
        }

        var text = textProp.GetString() ?? string.Empty;
        var result = await _backend.TypeAsync(text, ct).ConfigureAwait(false);
        return new ToolResult(result.Success, result.Message, result.Data);
    }
}
