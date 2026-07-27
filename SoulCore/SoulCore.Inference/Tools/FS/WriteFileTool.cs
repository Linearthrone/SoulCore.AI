using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.FS;

namespace SoulCore.Inference.Tools.FS;

/// <summary>
/// <c>write_file</c> tool — writes a text file to a whitelisted write root.
/// Enforces <see cref="ToolsOptions.FilesystemWriteRoots"/> (a subset of read roots).
/// Rejects writes to read-only roots, escapes via <c>../</c>, absolute paths
/// outside roots, and symlinks pointing out. Creates the parent directory
/// if missing. Empty <see cref="ToolsOptions.FilesystemWriteRoots"/> disables
/// the tool entirely.
/// </summary>
public sealed class WriteFileTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"path":{"type":"string","description":"Path to write (whitelisted write roots only)."},"content":{"type":"string","description":"Text content to write."}},"required":["path","content"]}""")
        .RootElement.Clone();

    private readonly ToolsOptions _options;
    private readonly IReadOnlyList<string> _writeRoots;

    public WriteFileTool(IOptions<ToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _writeRoots = BuildWriteRoots(_options);
    }

    public ToolDefinition Definition { get; } = new(
        Name: "write_file",
        Description: "Write a text file (whitelisted write roots only).",
        Parameters: Parameters);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (_writeRoots.Count == 0)
            return Task.FromResult(new ToolResult(false, "filesystem tools disabled", null));

        var path = args.TryGetProperty("path", out var p) ? p.GetString() : null;
        var content = args.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;

        var resolved = FilesystemGuard.TryResolve(path, _writeRoots, out var reason);
        if (resolved is null)
            return Task.FromResult(new ToolResult(false, reason, null));

        try
        {
            var dir = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(resolved, content);
            return Task.FromResult(new ToolResult(true, $"wrote {content.Length} chars to {path}", new { path = resolved, bytes = content.Length }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"write_file failed: {ex.Message}", null));
        }
    }

    internal static IReadOnlyList<string> BuildWriteRoots(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.FilesystemWriteRoots.Count > 0)
            return ReadFileTool.Canonicalize(options.FilesystemWriteRoots);
        if (!options.UseDefaultRoots)
            return Array.Empty<string>();

        // Default write roots = the read/write portion of defaults (NOT memory — read-only).
        var defaults = FilesystemGuard.DefaultPackageRelativeRoots(
            includeQaGlob: true, includeScratch: true);
        return ReadFileTool.Canonicalize(defaults);
    }
}
