using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_screenshot</c> — read-only capture. Gated by
/// <see cref="IComputerControlGate.AllowDesktopCapture"/> (default true).
/// </summary>
public sealed class DesktopScreenshotTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "monitor": {
              "type": "integer",
              "description": "Monitor index (0 = primary / virtual screen).",
              "default": 0
            }
          }
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopScreenshotTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_screenshot",
        Description: "Capture the desktop screen.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowDesktopCapture)
            return DesktopToolGate.RefuseCapture();

        var monitor = 0;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("monitor", out var m)
            && m.ValueKind == JsonValueKind.Number
            && m.TryGetInt32(out var mi)
            && mi >= 0)
        {
            monitor = mi;
        }

        var result = await _backend.ScreenshotAsync(monitor, ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
