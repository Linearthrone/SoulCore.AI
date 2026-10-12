using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Keeps guest file and shell paths inside the Ubuntu user's home.
/// Lexical only — the guest process rechecks <c>realpath</c> before it acts.
/// </summary>
public static class GuestPathGuard
{
    public const int MaxReadChars = 48_000;
    public const int MaxWriteChars = 200_000;
    public const int MaxShellChars = 4_000;
    public const int MaxShellOutputChars = 12_000;
    public const int DefaultShellTimeoutSeconds = 60;
    public const int MaxShellTimeoutSeconds = 180;

    private static readonly Regex SafeUser = new(
        @"^[A-Za-z0-9_-]{1,32}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DeniedShell = new(
        @"(?:^|[;&|`\n])\s*(sudo|pkexec|su|passwd|doas)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// PROP-17.1 / 17.2: refuse package install and pipe-to-bash in every mode
    /// (apt, snap, flatpak, wget|bash / curl|bash). Compile/test under /home/victoria stays.
    /// </summary>
    private static readonly Regex DeniedPackageOrPipeBash = new(
        @"\b(?:apt(?:-get)?|aptitude|snap|flatpak)\b.*\b(?:install|download|add)\b" +
        @"|\b(?:install|download|add)\b.*\b(?:apt(?:-get)?|aptitude|snap|flatpak)\b" +
        @"|\b(?:wget|curl)\b[^|;]*\|\s*(?:ba)?sh\b" +
        @"|\b(?:wget|curl)\b[^;\n]*\s\|\s*(?:/bin/)?(?:ba)?sh\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DeniedBrowserInstall = new(
        @"\b(?:apt(?:-get)?|snap|flatpak)\b.*\b(?:firefox|chrome|chromium|brave|opera|vivaldi|microsoft-edge|google-chrome)\b" +
        @"|\b(?:firefox|chrome|chromium|brave)\b.*\b(?:install)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryHome(string? username, out string home, out string error)
    {
        var user = (username ?? "").Trim();
        if (user.Length == 0)
            user = "victoria";
        if (!SafeUser.IsMatch(user))
        {
            home = "";
            error = "guest username is not a safe home path";
            return false;
        }

        home = "/home/" + user;
        error = "";
        return true;
    }

    public static bool TryResolve(string? path, string home, out string resolved, out string error)
    {
        resolved = "";
        error = "";
        home = (home ?? "").TrimEnd('/');
        if (home.Length == 0 || !home.StartsWith('/'))
        {
            error = "guest home is not set";
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "path must be non-empty";
            return false;
        }

        var raw = path.Trim().Replace('\\', '/');
        if (raw.IndexOf('\0') >= 0 || raw.IndexOf('\n') >= 0 || raw.IndexOf('\r') >= 0)
        {
            error = "path contains control characters";
            return false;
        }

        var combined = raw.StartsWith('/')
            ? raw
            : home + "/" + raw.TrimStart('/');

        var stack = new List<string>();
        foreach (var part in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                if (stack.Count == 0)
                {
                    error = "path escapes the guest home";
                    return false;
                }

                stack.RemoveAt(stack.Count - 1);
                continue;
            }

            stack.Add(part);
        }

        var homeParts = home.Trim('/').Split('/');
        if (stack.Count < homeParts.Length)
        {
            error = "path escapes the guest home";
            return false;
        }

        for (var i = 0; i < homeParts.Length; i++)
        {
            if (!string.Equals(stack[i], homeParts[i], StringComparison.Ordinal))
            {
                error = "path escapes the guest home";
                return false;
            }
        }

        resolved = "/" + string.Join('/', stack);
        return true;
    }

    public static bool TryValidateCommand(string? command, out string error)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            error = "command must be non-empty";
            return false;
        }

        if (command.Length > MaxShellChars)
        {
            error = "command is too long";
            return false;
        }

        if (command.IndexOf('\0') >= 0)
        {
            error = "command contains a NUL";
            return false;
        }

        if (DeniedShell.IsMatch(command))
        {
            error =
                "guest_shell will not run sudo, su, pkexec, doas, or passwd — " +
                "a password prompt would hang the guest session. " +
                "Ask Kayleigh before installing packages.";
            return false;
        }

        if (DeniedPackageOrPipeBash.IsMatch(command) || DeniedBrowserInstall.IsMatch(command))
        {
            error =
                "guest_shell refused: package install and pipe-to-bash are blocked in every mode " +
                "(apt, snap, flatpak, wget|bash, curl|bash), including browser installs. " +
                "Compile and test under /home/victoria stay allowed. Ask Kayleigh for host-side installs.";
            return false;
        }

        error = "";
        return true;
    }

    public static string BashSingleQuote(string value)
        => "'" + (value ?? "").Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
