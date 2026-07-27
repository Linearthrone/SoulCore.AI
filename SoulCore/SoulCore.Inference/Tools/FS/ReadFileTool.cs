using System.Text.Json;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.FS;

namespace SoulCore.Inference.Tools.FS;

/// <summary>
/// <c>read_file</c> tool — reads a text file from a whitelisted root.
/// Rejects any path that escapes <see cref="ToolsOptions.FilesystemRoots"/>
/// (resolve + canonicalize + prefix check; symlinks pointing out are rejected).
/// Empty <see cref="ToolsOptions.FilesystemRoots"/> disables the tool entirely.
/// </summary>
public sealed class ReadFileTool : ITool
{
    private static readonly JsonElement Parameters = JsonDocument.Parse(
        """{"type":"object","properties":{"path":{"type":"string","description":"Path to a text file (whitelisted roots only)."}},"required":["path"]}""")
        .RootElement.Clone();

    private readonly ToolsOptions _options;
    private readonly IReadOnlyList<string> _roots;

    public ReadFileTool(IOptions<ToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _roots = BuildRoots(_options);
    }

    public ToolDefinition Definition { get; } = new(
        Name: "read_file",
        Description: "Read a text file (whitelisted roots only).",
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
            if (!File.Exists(resolved))
                return Task.FromResult(new ToolResult(false, $"file not found: {path}", null));

            var content = File.ReadAllText(resolved);
            return Task.FromResult(new ToolResult(true, content, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(false, $"read_file failed: {ex.Message}", null));
        }
    }

    internal static IReadOnlyList<string> BuildRoots(ToolsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.FilesystemRoots.Count > 0)
            return Canonicalize(options.FilesystemRoots);
        if (!options.UseDefaultRoots)
            return Array.Empty<string>();

        // Default roots — memory is read-only, qa-* and scratch are read/write.
        // Relative SoulCore/... entries are anchored to the package root (the
        // directory that contains SoulCore.sln) so they survive `dotnet run`
        // from Host/bin as well as repo-root cwd.
        var defaults = new List<string> { "%LOCALAPPDATA%/SoulCore/memory/" };
        defaults.AddRange(FilesystemGuard.DefaultPackageRelativeRoots(
            includeQaGlob: true, includeScratch: true));
        return Canonicalize(defaults);
    }

    /// <summary>
    /// Flatten each configured root through
    /// <see cref="FilesystemGuard.CanonicalizeRoot"/> (which may expand globs
    /// into multiple concrete roots).
    /// </summary>
    internal static IReadOnlyList<string> Canonicalize(IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
            return Array.Empty<string>();

        var list = new List<string>(roots.Count);
        foreach (var root in roots)
        {
            foreach (var c in FilesystemGuard.CanonicalizeRoot(root))
            {
                if (!string.IsNullOrEmpty(c))
                    list.Add(c);
            }
        }
        return list;
    }
}