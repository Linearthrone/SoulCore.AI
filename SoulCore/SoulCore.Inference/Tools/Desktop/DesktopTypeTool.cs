using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_type</c> — requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// </summary>
public sealed class DesktopTypeTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "text": { "type": "string", "description": "Text to type at the current focus." }
          },
          "required": ["text"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopTypeTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_type",
        Description: "Type text at the current focus.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("text", out var t)
            || t.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "error: desktop_type requires 'text' (string).", null);
        }

        var text = t.GetString();
        if (string.IsNullOrEmpty(text))
            return new ToolResult(false, "error: desktop_type 'text' must be non-empty.", null);

        var result = await _backend.TypeAsync(text, ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
