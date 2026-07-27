using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SoulCore.Inference.Tools.FS;

/// <summary>
/// Whitelist enforcement for the filesystem tools (BED-133 security gate).
/// Resolves a user-supplied path against the configured <see cref="SoulCore.Config.ToolsOptions.FilesystemRoots"/>,
/// canonicalizes it, follows symlinks/junctions to their final target, and
/// rejects any path that escapes the whitelist. All escape attempts return
/// <c>null</c> + a human-readable reason; the caller turns that into a failed
/// <c>ToolResult</c> rather than throwing.
/// </summary>
internal static class FilesystemGuard
{
    /// <summary>
    /// Resolve and validate <paramref name="rawPath"/> against <paramref name="roots"/>.
    /// Returns the canonical absolute path (with symlinks/junctions resolved to
    /// their final target) when the path is inside one of <paramref name="roots"/>;
    /// otherwise <c>(null, reason)</c>.
    /// </summary>
    /// <param name="rawPath">Model-supplied path (may be relative, absolute, or contain <c>..</c>).</param>
    /// <param name="roots">Whitelisted canonical roots (already env-expanded + canonicalized).</param>
    /// <param name="reason">Failure reason when the path is rejected.</param>
    public static string? TryResolve(string? rawPath, IReadOnlyList<string> roots, out string reason)
    {
        if (roots.Count == 0)
        {
            reason = "filesystem tools disabled";
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            reason = "path is empty";
            return null;
        }

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(rawPath.Trim());
        }
        catch (Exception)
        {
            reason = $"path could not be expanded: {rawPath}";
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(expanded);
        }
        catch (Exception)
        {
            reason = $"path could not be resolved: {rawPath}";
            return null;
        }

        // Resolve symlinks/junctions to their final target. If the link target
        // itself escapes the whitelist, the prefix check below catches it.
        // For not-yet-existing write targets, we resolve the parent (which
        // must exist) and re-append the leaf so the prefix check is honest.
        var canonical = ResolveFinalPath(fullPath);

        foreach (var root in roots)
        {
            var rootCanonical = ResolveFinalPath(root);
            if (IsInside(canonical, rootCanonical))
            {
                reason = string.Empty;
                return canonical;
            }
        }

        reason = $"path not in whitelisted roots: {rawPath}";
        return null;
    }

    /// <summary>
    /// Canonicalize a root string: env-expand, full-path, and resolve symlinks/
    /// junctions. Adds a trailing directory separator so prefix matching is
    /// directory-scoped. Best-effort — falls back to the literal canonical path
    /// when resolution fails (e.g. non-existent root is fine for prefix checks
    /// against files we're about to create under it).
    /// </summary>
    public static string CanonicalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(root.Trim());
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(expanded);
        }
        catch (Exception)
        {
            return string.Empty;
        }

        var canonical = ResolveFinalPath(fullPath);
        return EnsureTrailingSep(canonical);
    }

    /// <summary>
    /// Resolve a path to its final canonical form, following symlinks and
    /// junctions. For existing files/dirs, uses the OS final-path resolution
    /// (GetFinalPathByHandle on Windows). For not-yet-existing paths, resolves
    /// the existing parent and re-appends the leaf. Falls back to
    /// <see cref="Path.GetFullPath"/> when reparse-point resolution is
    /// unavailable or the path doesn't exist.
    /// </summary>
    private static string ResolveFinalPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        try
        {
            if (File.Exists(path))
                return ResolveExistingFinalPath(path, isDirectory: false);

            if (Directory.Exists(path))
                return ResolveExistingFinalPath(path, isDirectory: true);

            // Not-yet-existing target (e.g. write_file about to create it):
            // resolve the parent if it exists, then re-append the leaf so a
            // symlink in the parent chain is followed.
            var parent = Path.GetDirectoryName(path);
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                var parentFinal = ResolveExistingFinalPath(parent, isDirectory: true);
                return Path.Combine(parentFinal, leaf);
            }
        }
        catch (Exception)
        {
            // fall through to literal GetFullPath
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>
    /// Resolve an existing file/directory to its final path, following any
    /// reparse points (symlinks, junctions). On Windows uses
    /// <c>GetFinalPathNameByHandle</c>; on other platforms falls back to
    /// <see cref="Path.GetFullPath"/> (which already resolves POSIX symlinks
    /// for existing paths via the OS).
    /// </summary>
    private static string ResolveExistingFinalPath(string path, bool isDirectory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return WindowsFinalPathResolver.Resolve(path, isDirectory);
            }
            catch (Exception)
            {
                // fall through to GetFullPath
            }
        }

        return Path.GetFullPath(path);
    }

    private static bool IsInside(string candidate, string rootWithTrailingSep)
    {
        if (string.IsNullOrEmpty(rootWithTrailingSep))
            return false;

        var c = EnsureTrailingSep(candidate);
        return c.StartsWith(rootWithTrailingSep, RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    private static string EnsureTrailingSep(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

/// <summary>
/// Windows-only final-path resolution via <c>GetFinalPathNameByHandle</c>.
/// Opens a safe handle on the file/directory (no read/write — just metadata),
/// asks the OS for the canonical final path (all reparse points resolved),
/// and closes the handle. Used by <see cref="FilesystemGuard"/> to defeat
/// symlink/junction escapes that <see cref="Path.GetFullPath"/> misses.
/// </summary>
internal static class WindowsFinalPathResolver
{
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    public static string Resolve(string path, bool isDirectory)
    {
        // For directories we use BACKUP_SEMANTICS so CreateFile succeeds on dirs.
        // For files we add OPEN_REPARSE_POINT so we open the link itself, then
        // GetFinalPathNameByHandle follows it.
        var flags = FILE_FLAG_BACKUP_SEMANTICS;
        if (!isDirectory)
            flags |= FILE_FLAG_OPEN_REPARSE_POINT;

        var handle = CreateFileW(path, 0, FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero, FileMode.Open, (int)flags, IntPtr.Zero);

            if (handle.IsInvalid)
                throw new global::System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var buf = new StringBuilder(260);
            int size;
            while ((size = GetFinalPathNameByHandle(handle, buf, buf.Capacity, 0)) > buf.Capacity)
            {
                buf.Capacity = size;
            }
                if (size == 0)
                    throw new global::System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            var final = buf.ToString();
            // GetFinalPathNameByHandle returns a \\?\-prefixed path on Windows.
            // Strip the prefix for consistent comparison with Path.GetFullPath output.
            const string verbatim = @"\\?\";
            if (final.StartsWith(verbatim, StringComparison.OrdinalIgnoreCase))
                final = final[verbatim.Length..];
            // Normalize separators + canonicalize.
            return Path.GetFullPath(final);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, FileShare dwShareMode,
        IntPtr lpSecurityAttributes, FileMode dwCreationDisposition,
        int dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
    private static extern int GetFinalPathNameByHandle(
        SafeFileHandle hFile, StringBuilder lpszFilePath, int cchFilePath, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(SafeFileHandle hObject);
}
