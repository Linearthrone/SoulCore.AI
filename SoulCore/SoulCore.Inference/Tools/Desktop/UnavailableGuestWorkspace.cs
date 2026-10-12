namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Used when desktop tools are not scoped to a VirtualBox guest.
/// </summary>
public sealed class UnavailableGuestWorkspace : IGuestWorkspace
{
    public const string Message =
        "guest file and shell tools need DesktopTargetWindowTitle set to the VirtualBox guest (victoria-sandbox).";

    public string Home => "/home/victoria";

    public Task<DesktopOpResult> ListAsync(string path, CancellationToken ct = default)
        => Refuse();

    public Task<DesktopOpResult> ReadAsync(string path, CancellationToken ct = default)
        => Refuse();

    public Task<DesktopOpResult> WriteAsync(string path, string content, CancellationToken ct = default)
        => Refuse();

    public Task<DesktopOpResult> ShellAsync(
        string command, string? cwd, int timeoutSeconds, CancellationToken ct = default)
        => Refuse();

    private static Task<DesktopOpResult> Refuse()
        => Task.FromResult(new DesktopOpResult(false, Message, null));
}
