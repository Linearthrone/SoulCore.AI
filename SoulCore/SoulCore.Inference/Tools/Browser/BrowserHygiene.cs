using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.Browser;

/// <summary>
/// PROP-17.2 code gates: one live tab per URL, refuse second browser, refuse install-a-browser.
/// Authority: tool code → charter → operator settings → notes. An SOP cannot flip these gates.
/// </summary>
public static class BrowserHygiene
{
    public const string SecondBrowserRefused =
        "refused: a browser is already open. One browser only — reuse the existing window/tabs. " +
        "Do not install or launch a second browser.";

    public const string BrowserInstallRefused =
        "refused: installing a browser is blocked. Use the existing browser and browser_navigate / browser_tabs.";

    public const string SiteDownParkHint =
        "site_down: park the task (PROP-17.1) — do not refresh-loop. failure_code=surface_browser_down";

    private static readonly Regex BrowserProcess = new(
        @"\b(?:firefox|chrome|chromium|google-chrome|msedge|brave|opera|vivaldi)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string NormalizeUrlKey(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return url.Trim().TrimEnd('/').ToLowerInvariant();
        var builder = new UriBuilder(uri)
        {
            Fragment = "",
            Port = uri.IsDefaultPort ? -1 : uri.Port
        };
        var path = builder.Path.TrimEnd('/');
        if (path.Length == 0)
            path = "/";
        builder.Path = path;
        return builder.Uri.AbsoluteUri.TrimEnd('/').ToLowerInvariant();
    }

    public static bool UrlsMatch(string? a, string? b)
    {
        var ka = NormalizeUrlKey(a);
        var kb = NormalizeUrlKey(b);
        return ka.Length > 0 && string.Equals(ka, kb, StringComparison.Ordinal);
    }

    public static bool LooksLikeBrowserProcess(string? nameOrLine) =>
        !string.IsNullOrWhiteSpace(nameOrLine) && BrowserProcess.IsMatch(nameOrLine);

    public static bool LooksLikeBrowserInstall(string? command) =>
        !string.IsNullOrWhiteSpace(command)
        && BrowserProcess.IsMatch(command)
        && (command.Contains("install", StringComparison.OrdinalIgnoreCase)
            || command.Contains("apt", StringComparison.OrdinalIgnoreCase)
            || command.Contains("snap", StringComparison.OrdinalIgnoreCase)
            || command.Contains("flatpak", StringComparison.OrdinalIgnoreCase));
}
