namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Files and a non-root shell inside the Ubuntu guest home (VSCodium's project tree).
/// </summary>
public interface IGuestWorkspace
{
    string Home { get; }

    Task<DesktopOpResult> ListAsync(string path, CancellationToken ct = default);

    Task<DesktopOpResult> ReadAsync(string path, CancellationToken ct = default);

    Task<DesktopOpResult> WriteAsync(string path, string content, CancellationToken ct = default);

    Task<DesktopOpResult> ShellAsync(
        string command, string? cwd, int timeoutSeconds, CancellationToken ct = default);
}
