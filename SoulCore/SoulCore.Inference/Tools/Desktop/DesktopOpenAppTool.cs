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
              "description": "App alias: notepad, explorer, cmd, powershell; or firefox/chrome/edge to open the guest browser when VM-scoped (not Playwright)."
            },
            "args": {
              "type": "string",
              "description": "Optional arguments (e.g. URL for guest Firefox)."
            }
          },
          "required": ["app"]
        }
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;
    private readonly IToolsAccessSettings? _access;

    public DesktopOpenAppTool(
        IComputerControlGate gate,
        IDesktopControlBackend backend,
        IToolsAccessSettings? access = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _access = access ?? gate as IToolsAccessSettings;
    }

    /// <summary>
    /// chrome/edge/firefox map to guest Firefox inside VirtualBox. When Playwright
    /// is the browser backend those aliases must never launch.
    /// </summary>
    public static bool IsBrowserAlias(string? app)
    {
        var alias = (app ?? string.Empty).Trim().ToLowerInvariant();
        return alias is "chrome" or "google chrome" or "msedge" or "edge" or "microsoft edge"
            or "firefox" or "browser" or "chromium";
    }

    public ToolDefinition Definition { get; } = new(
        Name: "desktop_open_app",
        Description:
            "Launch an allowlisted app. When DesktopTargetWindowTitle scopes to victoria-sandbox and BrowserBackend is not playwright, " +
            "chrome/edge/firefox open guest Firefox inside the VM (pass URL in args). " +
            "codium/vscodium/code opens VSCodium in the guest. Otherwise non-browser aliases only " +
            "(notepad, explorer, cmd, powershell). Requires AllowComputerControl.",
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

        if (IsBrowserAlias(app)
            && DesktopToolIntent.IsPlaywrightBackend(_access?.BrowserBackend))
        {
            return new ToolResult(
                false,
                "error: refused desktop_open_app '" + app.Trim() + "'. That alias opens Firefox inside VirtualBox. " +
                "Websites go to browser_navigate (Victoria's Playwright Chromium). Do not mention Firefox or VirtualBox.",
                null);
        }

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
