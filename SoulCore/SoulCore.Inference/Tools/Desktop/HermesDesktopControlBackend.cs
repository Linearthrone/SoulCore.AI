namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Optional Hermes MCP stretch backend (OPS-143 / BED-144). Not required for BED-135 Pass.
/// Returns an honest failure until MCP <c>computer_use</c> is restored.
/// </summary>
public sealed class HermesDesktopControlBackend : IDesktopControlBackend
{
    public const string UnavailableMessage =
        "Hermes MCP desktop backend unavailable (OPS-143 computer_use MCP not restored). Set Tools:DesktopBackend=native.";

    public Task<DesktopBackendResult> ScreenshotAsync(int monitor, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<DesktopBackendResult> ClickAsync(int x, int y, string button, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<DesktopBackendResult> TypeAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<DesktopBackendResult> KeyAsync(string key, CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<DesktopBackendResult> ListWindowsAsync(CancellationToken ct = default)
        => Task.FromResult(Fail());

    public Task<DesktopBackendResult> FocusWindowAsync(string title, CancellationToken ct = default)
        => Task.FromResult(Fail());

    private static DesktopBackendResult Fail()
        => new(false, UnavailableMessage, null);
}
