using System.Text.Json;
using SoulCore.Inference.Tooling;

namespace SoulCore.Inference.Tools.Desktop;

public sealed class GuestListDirTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"path":{"type":"string","description":"Directory inside the Ubuntu guest home. Empty lists /home/victoria. Relative paths are under that home."}},"required":[]}
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IGuestWorkspace _workspace;

    public GuestListDirTool(IComputerControlGate gate, IGuestWorkspace workspace)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "guest_list_dir",
        Description:
            "List a directory inside the Ubuntu guest (victoria-sandbox), under /home/victoria. " +
            "Use this for the VSCodium project tree instead of clicking the file explorer. " +
            "Requires AllowComputerControl.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();
        var path = ReadString(args, "path") ?? "";
        return DesktopToolGate.FromBackend(await _workspace.ListAsync(path, ct).ConfigureAwait(false));
    }

    internal static string? ReadString(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }
}

public sealed class GuestReadFileTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"path":{"type":"string","description":"Text file inside the Ubuntu guest home, e.g. booklist/src/App.jsx"}},"required":["path"]}
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IGuestWorkspace _workspace;

    public GuestReadFileTool(IComputerControlGate gate, IGuestWorkspace workspace)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "guest_read_file",
        Description:
            "Read a text file inside the Ubuntu guest home (the VSCodium project). " +
            "Paths stay under /home/victoria. Does not read the Windows host. " +
            "Requires AllowComputerControl.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();
        var path = GuestListDirTool.ReadString(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            return new ToolResult(false, "error: guest_read_file requires 'path'.", null);
        var result = await _workspace.ReadAsync(path, ct).ConfigureAwait(false);
        if (result.Success && string.IsNullOrEmpty(result.Content))
            return new ToolResult(true, "(empty file)", result.Data);
        return DesktopToolGate.FromBackend(result);
    }
}

public sealed class GuestWriteFileTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"path":{"type":"string","description":"Text file to write inside the Ubuntu guest home."},"content":{"type":"string","description":"Full text content to write."}},"required":["path","content"]}
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IGuestWorkspace _workspace;

    public GuestWriteFileTool(IComputerControlGate gate, IGuestWorkspace workspace)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "guest_write_file",
        Description:
            "Write a text file inside the Ubuntu guest home so VSCodium shows the same file. " +
            "Creates parent folders. Paths stay under /home/victoria. " +
            "Requires AllowComputerControl.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();
        var path = GuestListDirTool.ReadString(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            return new ToolResult(false, "error: guest_write_file requires 'path'.", null);
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("content", out var contentEl)
            || contentEl.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(false, "error: guest_write_file requires 'content' (string).", null);
        }

        var result = await _workspace.WriteAsync(path, contentEl.GetString() ?? "", ct).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}

public sealed class GuestShellTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """
        {"type":"object","properties":{"command":{"type":"string","description":"Shell command to run as the guest user. Sudo only when AllowGuestSudo is on."},"cwd":{"type":"string","description":"Working directory under /home/victoria. Defaults to the home directory."},"timeout_seconds":{"type":"integer","description":"1-180. Default 60."}},"required":["command"]}
        """).RootElement.Clone();

    private readonly IComputerControlGate _gate;
    private readonly IToolsAccessSettings _access;
    private readonly IGuestWorkspace _workspace;

    public GuestShellTool(
        IComputerControlGate gate,
        IToolsAccessSettings access,
        IGuestWorkspace workspace)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "guest_shell",
        Description:
            "Run a command in the Ubuntu guest as user victoria, cwd under /home/victoria. " +
            "Use for npm, node, git, and builds in the VSCodium project. " +
            "Sudo/su require AllowGuestSudo (Settings → Tools & Access) and NOPASSWD in the guest. " +
            "Package install (apt/snap/flatpak) and pipe-to-bash stay refused. " +
            "Requires AllowComputerControl.",
        Parameters: Parameters);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!_gate.AllowComputerControl)
            return DesktopToolGate.RefuseControl();
        var command = GuestListDirTool.ReadString(args, "command");
        if (string.IsNullOrWhiteSpace(command))
            return new ToolResult(false, "error: guest_shell requires 'command'.", null);
        var cwd = GuestListDirTool.ReadString(args, "cwd");
        var timeout = GuestPathGuard.DefaultShellTimeoutSeconds;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("timeout_seconds", out var t)
            && t.ValueKind == JsonValueKind.Number
            && t.TryGetInt32(out var seconds))
        {
            timeout = seconds;
        }

        var result = await _workspace.ShellAsync(
            command,
            cwd,
            timeout,
            ct,
            allowElevatedShell: _access.AllowGuestSudo).ConfigureAwait(false);
        return DesktopToolGate.FromBackend(result);
    }
}
