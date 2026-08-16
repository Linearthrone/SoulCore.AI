using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>desktop_open_app</c> — launch an allowlisted local app (BED-174).
/// Requires session <see cref="IComputerControlGate.AllowComputerControl"/>.
/// </summary>
public sealed class DesktopOpenAppTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "app": {
              "type": "string",
              "description": "Allowlisted app alias: chrome, edge, firefox, notepad, explorer, cmd, powershell."
            },
            "args": {
              "type": "string",
              "description": "Optional arguments. For browsers, a URL (https://…) opens that page."
            }
          },
          "required": ["app"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public DesktopOpenAppTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_open_app",
        Description:
            "Launch an allowlisted local desktop app (chrome, edge, firefox, notepad, explorer, cmd, powershell). " +
            "Use this to open Google Chrome / Edge / Notepad — do not invent terminal or browser_navigate tools. " +
            "Optional args: a URL for browsers (e.g. https://google.com). Requires AllowComputerControl.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("app", out var a)
            || a.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "error: desktop_open_app requires 'app' (string).", null);
        }

        var app = a.GetString();
        if (string.IsNullOrWhiteSpace(app))
            return new ToolResult(false, "error: desktop_open_app 'app' must be non-empty.", null);

        string? launchArgs = null;
        if (args.TryGetProperty("args", out var argEl) && argEl.ValueKind == JsonValueKind.String)
        {
            var s = argEl.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                launchArgs = s;
        }

        var result = await _backend.OpenAppAsync(app, launchArgs, ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
