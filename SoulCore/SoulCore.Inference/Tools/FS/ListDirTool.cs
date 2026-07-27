using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.FS;
using StringBuilder = System.Text.StringBuilder;

namespace SoulCore.Inference.Tools.FS;

/// <summary>
/// <c>list_dir</c> tool — lists files in a whitelisted directory.
/// Same whitelist enforcement as <see cref="ReadFileTool"/>: resolve +
/// canonicalize + prefix check; symlinks pointing out are rejected.
/// Returns one entry per line (files and subdirectories; non-recursive).
/// Empty <see cref="ToolsOptions.FilesystemRoots"/> disables the tool entirely.
/// </summary>
public sealed class ListDirTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"path":{"type":"string","description":"Directory to list (whitelisted roots only)."}},"required":["path"]}""")
        .RootElement.Clone();

    private readonly ToolsOptions _options;
    private readonly IReadOnlyList<string> _roots;

    public ListDirTool(IOptions<ToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _roots = ReadFileTool.BuildRoots(_options);
    }

    public ToolDefinition Definition { get; } = new(
        Name: "list_dir",
        Description: "List files in a directory (whitelisted roots only).",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (_roots.Count == 0)
            return Task.FromResult(new ToolResult(false, "filesystem tools disabled", null));

        var path = args.TryGetProperty("path", out var p) ? p.GetString() : null;
        var resolved = FilesystemGuard.TryResolve(path, _roots, out var reason);
        if (resolved is null)
            return Task.FromResult(new ToolResult(false, reason, null));

        try
        {
            if (!Directory.Exists(resolved))
                return Task.FromResult(new ToolResult(false, $"directory not found: {path}", null));

            var sb = new StringBuilder();
            foreach (var entry in Directory.EnumerateFileSystemEntries(resolved).OrderBy(e => e, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(entry);
                var isDir = Directory.Exists(entry);
                sb.Append(isDir ? "dir  " : "file ").Append(name).Append('\n');
            }

            var content = sb.ToString();
            if (content.Length == 0)
                content = "(empty directory)";

            return Task.FromResult(new ToolResult(true, content, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"list_dir failed: {ex.Message}", null));
        }
    }
}
