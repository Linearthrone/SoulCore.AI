using System.Globalization;

namespace House.ChatDesktop.Services;

/// <summary>PROP-15.22 — title-bar / Settings copy for Presence + Host currency.</summary>
public static class BuildCurrencyStatus
{
    public const string HostNoPersonaApiMessage =
        "Host has no persona API — restart CreatorEdition Host";

    public const string HostOutdatedShort = "Host outdated";

    public const string HostPersonasOkShort = "Personas OK";

    public static string PresenceCurrencyLabel(
        PresenceUpdateCheckResult.Kind? status,
        string? availableVersion,
        bool isInstalled)
    {
        if (!isInstalled || status == PresenceUpdateCheckResult.Kind.DevBuild)
            return "Unpackaged";
        return status switch
        {
            PresenceUpdateCheckResult.Kind.Available =>
                string.IsNullOrWhiteSpace(availableVersion)
                    ? "Update available"
                    : $"Update available: v{availableVersion.Trim()}",
            PresenceUpdateCheckResult.Kind.UpToDate => "Up to date",
            PresenceUpdateCheckResult.Kind.FeedEmpty => "No releases yet",
            PresenceUpdateCheckResult.Kind.Failed => "Update check failed",
            _ => isInstalled ? "Installed" : "Unpackaged"
        };
    }

    /// <summary>
    /// Host is stale for CreatorEdition personas when /api/personas is 404,
    /// /health omits the persona object, or capabilities.personas is missing/false.
    /// Any fail → outdated; null signals are unknown (not outdated).
    /// </summary>
    public static bool IsHostOutdatedForPersonas(
        bool? personaApiOk,
        bool? healthHasPersona,
        bool? healthCapabilitiesPersonas = null)
    {
        if (personaApiOk == false)
            return true;
        if (healthHasPersona == false)
            return true;
        if (healthCapabilitiesPersonas == false)
            return true;
        return false;
    }

    public static string HostCurrencyLabel(
        bool? personaApiOk,
        bool? healthHasPersona,
        bool hostBehindPresence,
        bool? healthCapabilitiesPersonas = null)
    {
        if (IsHostOutdatedForPersonas(personaApiOk, healthHasPersona, healthCapabilitiesPersonas))
            return $"{HostOutdatedShort} (restart from CreatorEdition tree)";
        if (hostBehindPresence)
            return "Host behind Presence";
        if (personaApiOk == true || healthHasPersona == true || healthCapabilitiesPersonas == true)
            return HostPersonasOkShort;
        return "Host status unknown";
    }

    public static string FormatTitleChrome(
        string presenceVersion,
        string? hostVersion,
        string presenceCurrency,
        string hostCurrencyShort)
    {
        var presence = string.IsNullOrWhiteSpace(presenceVersion) ? "—" : presenceVersion.Trim();
        var host = string.IsNullOrWhiteSpace(hostVersion) ? "—" : hostVersion.Trim();
        var pc = string.IsNullOrWhiteSpace(presenceCurrency) ? null : presenceCurrency.Trim();
        var hc = string.IsNullOrWhiteSpace(hostCurrencyShort) ? null : hostCurrencyShort.Trim();

        // Presence 0.2.1 · Up to date · Host 0.1.5 · Host outdated
        var parts = new List<string> { $"Presence {presence}" };
        if (pc is not null)
            parts.Add(pc);
        parts.Add($"Host {host}");
        if (hc is not null)
            parts.Add(hc);
        return string.Join(" · ", parts);
    }

    public static string HostCurrencyShort(
        bool? personaApiOk,
        bool? healthHasPersona,
        bool hostBehindPresence,
        bool? healthCapabilitiesPersonas = null)
    {
        if (IsHostOutdatedForPersonas(personaApiOk, healthHasPersona, healthCapabilitiesPersonas))
            return HostOutdatedShort;
        if (hostBehindPresence)
            return "Host behind";
        if (personaApiOk == true || healthHasPersona == true || healthCapabilitiesPersonas == true)
            return HostPersonasOkShort;
        return "";
    }

    /// <summary>True when Host version is strictly older than Presence (informational).</summary>
    public static bool IsHostBehindPresence(string? hostVersion, string? presenceVersion)
    {
        if (!TryParseVersion(hostVersion, out var host) || !TryParseVersion(presenceVersion, out var presence))
            return false;
        return host < presence;
    }

    public static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        var cut = s.IndexOfAny(['+', '-']);
        if (cut > 0)
            s = s[..cut];

        // Accept 0.1.5 / 0.1 / 1.2.3.4
        var parts = s.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 4)
            return false;

        var nums = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out nums[i]))
                return false;
        }

        version = parts.Length switch
        {
            2 => new Version(nums[0], nums[1]),
            3 => new Version(nums[0], nums[1], nums[2]),
            _ => new Version(nums[0], nums[1], nums[2], nums[3])
        };
        return true;
    }
}
