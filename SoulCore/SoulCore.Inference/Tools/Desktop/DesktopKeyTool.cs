using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_key</c> — requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// Supports single keys and chords like <c>Ctrl+L</c> (BED-174).
/// </summary>
public sealed class DesktopKeyTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "key": {
              "type": "string",
              "description": "Key or chord to press (e.g. Enter, Escape, Tab, Ctrl+L, Alt+Tab, Ctrl+T)."
            }
          },
          "required": ["key"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopKeyTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_key",
        Description:
            "Press a key or chord (Enter, Escape, Tab, Ctrl+L, Alt+Tab, Ctrl+T, etc.) " +
            "on the window you last clicked. Requires a prior desktop_click and AllowComputerControl.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("key", out var k)
            || k.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "error: desktop_key requires 'key' (string).", null);
        }

        var key = k.GetString();
        if (string.IsNullOrWhiteSpace(key))
            return new ToolResult(false, "error: desktop_key 'key' must be non-empty.", null);

        var result = await _backend.KeyAsync(key, ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
