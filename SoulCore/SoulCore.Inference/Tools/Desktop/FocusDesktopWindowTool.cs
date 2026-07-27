using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>focus_desktop_window</c> — read/focus. Gated by
/// <see cref="IComputerControlGate.AllowDesktopCapture"/> (same as capture —
/// focusing does not inject keyboard/mouse events beyond foreground activation).
/// </summary>
public sealed class FocusDesktopWindowTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Window title (or substring) to focus." }
          },
          "required": ["title"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public FocusDesktopWindowTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "focus_desktop_window",
        Description: "Focus a desktop window by title or index.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowDesktopCapture)
            return DesktopToolGate.RefuseCapture();

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("title", out var t)
            || t.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "error: focus_desktop_window requires 'title' (string).", null);
        }

        var title = t.GetString();
        if (string.IsNullOrWhiteSpace(title))
            return new ToolResult(false, "error: focus_desktop_window 'title' must be non-empty.", null);

        var result = await _backend.FocusWindowAsync(title, ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
