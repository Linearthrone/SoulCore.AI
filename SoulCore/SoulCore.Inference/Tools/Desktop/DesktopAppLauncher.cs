using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Allowlisted local app launch (BED-174). No unbounded shell — named aliases only.
/// Shared by native and cua desktop backends.
/// </summary>
public static class DesktopAppLauncher
{
    public readonly record struct ResolvedLaunch(
        string Alias,
        string FileName,
        string Arguments,
        bool UseShellExecute);

    /// <summary>
    /// Resolve an allowlisted app alias (+ optional args) without starting a process.
    /// </summary>
    public static bool TryResolve(string app, string? args, out ResolvedLaunch resolved, out string error)
    {
        resolved = default;
        error = "";

        if (string.IsNullOrWhiteSpace(app))
        {
            error = "app must be non-empty";
            return false;
        }

        var alias = NormalizeAlias(app);
        if (!TryMapAlias(alias, out var fileName, out var isBrowser))
        {
            error =
                $"app '{app.Trim()}' is not on the desktop allowlist " +
                "(chrome|edge|firefox|notepad|explorer|cmd|powershell)";
            return false;
        }

        var arguments = "";
        var trimmedArgs = string.IsNullOrWhiteSpace(args) ? null : args.Trim();
        if (trimmedArgs is not null)
        {
            if (isBrowser && LooksLikeUrl(trimmedArgs))
            {
                arguments = QuoteIfNeeded(trimmedArgs);
            }
            else if (isBrowser && LooksLikeUrl("https://" + trimmedArgs)
                     && !trimmedArgs.Contains(' ', StringComparison.Ordinal)
                     && trimmedArgs.Contains('.', StringComparison.Ordinal))
            {
                arguments = QuoteIfNeeded("https://" + trimmedArgs);
            }
            else
            {
                // Pass-through for explorer paths / notepad files — no shell metachar expansion.
                arguments = trimmedArgs;
            }
        }

        // Prefer a concrete path when found (more reliable than bare name).
        var resolvedPath = TryFindExecutable(fileName);
        resolved = new ResolvedLaunch(
            Alias: alias,
            FileName: resolvedPath ?? fileName,
            Arguments: arguments,
            UseShellExecute: true);
        return true;
    }

    /// <summary>
    /// Launch via <see cref="Process.Start"/> on Windows. Non-Windows → clear failure.
    /// </summary>
    public static DesktopOpResult Launch(string app, string? args)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DesktopOpResult(
                false,
                $"desktop open_app requires Windows (OS={RuntimeInformation.OSDescription})",
                null);
        }

        if (!TryResolve(app, args, out var resolved, out var error))
            return new DesktopOpResult(false, error, null);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = resolved.FileName,
                Arguments = resolved.Arguments,
                UseShellExecute = resolved.UseShellExecute,
            };
            var proc = Process.Start(psi);
            if (proc is null)
            {
                return new DesktopOpResult(
                    false,
                    $"failed to start '{resolved.Alias}' ({resolved.FileName}) — Process.Start returned null",
                    new { app = resolved.Alias, path = resolved.FileName, args = resolved.Arguments });
            }

            var note = string.IsNullOrEmpty(resolved.Arguments)
                ? $"opened app '{resolved.Alias}' ({resolved.FileName})"
                : $"opened app '{resolved.Alias}' ({resolved.FileName}) args={resolved.Arguments}";
            return new DesktopOpResult(
                true,
                note,
                new
                {
                    app = resolved.Alias,
                    path = resolved.FileName,
                    args = resolved.Arguments,
                    pid = proc.Id,
                });
        }
        catch (Exception ex)
        {
            return new DesktopOpResult(
                false,
                $"failed to start '{resolved.Alias}': {ex.GetType().Name}: {ex.Message}",
                new { app = resolved.Alias, path = resolved.FileName });
        }
    }

    public static bool IsAllowlisted(string app)
        => !string.IsNullOrWhiteSpace(app) && TryMapAlias(NormalizeAlias(app), out _, out _);

    public static IReadOnlyList<string> AllowlistAliases { get; } = new[]
    {
        "chrome", "msedge", "edge", "firefox", "notepad",
        "explorer", "file_explorer", "cmd", "powershell",
    };

    /// <summary>
    /// Infer allowlisted alias from NL like "open Google Chrome" / "launch browser".
    /// </summary>
    public static bool TryInferAliasFromUserText(string? userText, out string alias)
    {
        alias = "";
        if (string.IsNullOrWhiteSpace(userText))
            return false;

        var text = userText.Trim().ToLowerInvariant();
        // Order: longer / more specific phrases first.
        (string needle, string mapped)[] cues =
        [
            ("google chrome", "chrome"),
            ("microsoft edge", "msedge"),
            ("file explorer", "file_explorer"),
            ("chrome", "chrome"),
            ("msedge", "msedge"),
            ("edge", "msedge"),
            ("firefox", "firefox"),
            ("notepad", "notepad"),
            ("explorer", "explorer"),
            ("powershell", "powershell"),
            ("cmd", "cmd"),
            ("browser", "chrome"),
        ];

        foreach (var (needle, mapped) in cues)
        {
            if (text.Contains(needle, StringComparison.Ordinal))
            {
                alias = mapped;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeAlias(string app)
    {
        var s = app.Trim().ToLowerInvariant();
        s = s.Replace(".exe", "", StringComparison.Ordinal);
        s = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return s switch
        {
            "google chrome" or "google-chrome" or "chromebrowser" => "chrome",
            "microsoft edge" or "microsoft-edge" => "msedge",
            "file explorer" or "file-explorer" or "windows explorer" => "file_explorer",
            "pwsh" => "powershell",
            "browser" => "chrome",
            _ => s.Replace(' ', '_'),
        };
    }

    private static bool TryMapAlias(string alias, out string fileName, out bool isBrowser)
    {
        isBrowser = false;
        fileName = alias switch
        {
            "chrome" => "chrome.exe",
            "msedge" or "edge" => "msedge.exe",
            "firefox" => "firefox.exe",
            "notepad" => "notepad.exe",
            "explorer" or "file_explorer" => "explorer.exe",
            "cmd" => "cmd.exe",
            "powershell" => "powershell.exe",
            _ => "",
        };
        if (fileName.Length == 0)
            return false;
        isBrowser = fileName is "chrome.exe" or "msedge.exe" or "firefox.exe";
        return true;
    }

    private static bool LooksLikeUrl(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteIfNeeded(string value)
    {
        if (value.Contains(' ', StringComparison.Ordinal) && !value.StartsWith('\"'))
            return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        return value;
    }

    private static string? TryFindExecutable(string fileName)
    {
        // Common install locations (Windows). Bare name + ShellExecute remains the fallback.
        var candidates = new List<string>();

        void Add(string? root, params string[] parts)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            candidates.Add(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        }

        if (fileName.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase))
        {
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe");
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe");
            Add(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe");
        }
        else if (fileName.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase))
        {
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe");
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe");
        }
        else if (fileName.Equals("firefox.exe", StringComparison.OrdinalIgnoreCase))
        {
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Mozilla Firefox", "firefox.exe");
            Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Mozilla Firefox", "firefox.exe");
        }

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return null;
    }
}
