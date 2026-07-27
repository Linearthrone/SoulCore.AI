using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// <c>list_desktop_windows</c> — read-only. Gated by
/// <see cref="IComputerControlGate.AllowDesktopCapture"/>.
/// </summary>
public sealed class ListDesktopWindowsTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """{"type":"object","properties":{}}""").RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IDesktopControlBackend _backend;

    public ListDesktopWindowsTool(IComputerControlGate gate, IDesktopControlBackend backend)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "list_desktop_windows",
        Description: "List open desktop windows.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowDesktopCapture)
            return DesktopToolGate.RefuseCapture();

        var result = await _backend.ListWindowsAsync(ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
