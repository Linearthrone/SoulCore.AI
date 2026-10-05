using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace House.ChatDesktop.Services;

/// <summary>PROP-4.2: Velopack update checks for Presence (House.ChatDesktop).</summary>
public sealed class PresenceUpdateService
{
    /// <summary>Default feed — GitHub Releases for this repo (override with env / settings).</summary>
    public const string DefaultGithubRepoUrl = "https://github.com/Linearthrone/SoulCore.AI";

    public const string DefaultGithubApiReleasesUrl =
        "https://api.github.com/repos/Linearthrone/SoulCore.AI/releases?per_page=10";

    public string CurrentVersion { get; }

    public string FeedDescription { get; }

    public bool IsInstalled { get; }

    private readonly UpdateManager? _manager;
    private readonly string? _githubApiReleasesUrl;
    private readonly HttpClient? _http;

    public PresenceUpdateService(string? feedOverride = null, HttpClient? http = null)
    {
        CurrentVersion = ResolveVersion();
        var feed = ResolveFeed(feedOverride);
        FeedDescription = feed.Description;
        _githubApiReleasesUrl = feed.GithubApiReleasesUrl;
        _http = http;

        try
        {
            _manager = feed.Source is null
                ? new UpdateManager(feed.UrlOrPath!)
                : new UpdateManager(feed.Source);
            IsInstalled = _manager.IsInstalled;
        }
        catch
        {
            _manager = null;
            IsInstalled = false;
        }
    }

    public async Task<PresenceUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_manager is null || !_manager.IsInstalled)
        {
            return PresenceUpdateCheckResult.DevBuild(
                CurrentVersion,
                $"Update only works on an installed Presence (Setup.exe). " +
                $"This is an unpackaged build ({CurrentVersion}). " +
                "Pack + publish a release (pack-presence.ps1 -Publish), install Setup.exe, then Update works.");
        }

        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is not null)
            {
                var remote = info.TargetFullRelease?.Version?.ToString() ?? "newer";
                return PresenceUpdateCheckResult.Available(CurrentVersion, remote, info);
            }

            // Velopack treats an empty GitHub feed as "up to date" — call that out explicitly.
            if (!string.IsNullOrWhiteSpace(_githubApiReleasesUrl))
            {
                var feedState = await ProbeGithubPresenceFeedAsync(_githubApiReleasesUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (feedState == GithubFeedState.Empty)
                {
                    return PresenceUpdateCheckResult.FeedEmpty(
                        CurrentVersion,
                        "No Presence releases on GitHub yet — Presence step has nothing to download. " +
                        "Publish with: House/scripts/pack-presence.ps1 -Bump -Publish " +
                        "(or the Presence Release GitHub Action). Host still updates via chrome Update / Update now.");
                }
            }

            return PresenceUpdateCheckResult.UpToDate(CurrentVersion);
        }
        catch (Exception ex)
        {
            return PresenceUpdateCheckResult.Fail($"Update check failed: {ex.Message}");
        }
    }

    public async Task<PresenceUpdateApplyResult> DownloadAndApplyAsync(
        UpdateInfo update,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_manager is null)
            return PresenceUpdateApplyResult.Fail("Update manager unavailable.");

        try
        {
            await _manager.DownloadUpdatesAsync(update, progress).ConfigureAwait(false);
            _manager.ApplyUpdatesAndRestart(update);
            return PresenceUpdateApplyResult.Restarting();
        }
        catch (Exception ex)
        {
            return PresenceUpdateApplyResult.Fail($"Update apply failed: {ex.Message}");
        }
    }

    internal static string ResolveVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var informational = asm
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    internal static bool LooksLikePresenceReleaseAsset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var n = name.Trim();
        return n.Contains("HouseVictoria.Presence", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("releases.", StringComparison.OrdinalIgnoreCase)
            || (n.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                && n.Contains("Presence", StringComparison.OrdinalIgnoreCase))
            || string.Equals(n, "Setup.exe", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<GithubFeedState> ProbeGithubPresenceFeedAsync(string apiUrl, CancellationToken ct)
    {
        try
        {
            var http = _http ?? CreateGithubHttp();
            using var owned = _http is null ? http : null;
            using var resp = await http.GetAsync(apiUrl, ct).ConfigureAwait(false);
            if ((int)resp.StatusCode == 404)
                return GithubFeedState.Empty;
            if (!resp.IsSuccessStatusCode)
                return GithubFeedState.Unknown;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return GithubFeedState.Empty;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (LooksLikePresenceReleaseAsset(name))
                        return GithubFeedState.HasPresence;
                }
            }

            return GithubFeedState.Empty;
        }
        catch
        {
            return GithubFeedState.Unknown;
        }
    }

    private static HttpClient CreateGithubHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HouseVictoria-Presence", "1"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    private static Feed ResolveFeed(string? feedOverride)
    {
        var raw = (feedOverride
                   ?? Environment.GetEnvironmentVariable("HOUSE_VICTORIA_UPDATE_URL")
                   ?? Environment.GetEnvironmentVariable("SOULCORE_PRESENCE_UPDATE_URL")
                   ?? "").Trim();

        if (string.IsNullOrEmpty(raw))
        {
            return new Feed(
                Description: $"GitHub Releases ({DefaultGithubRepoUrl})",
                Source: new GithubSource(DefaultGithubRepoUrl, string.Empty, prerelease: false),
                UrlOrPath: null,
                GithubApiReleasesUrl: DefaultGithubApiReleasesUrl);
        }

        if (raw.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var api = TryGithubApiFromRepoUrl(raw) ?? DefaultGithubApiReleasesUrl;
            return new Feed(
                Description: $"GitHub Releases ({raw})",
                Source: new GithubSource(raw, string.Empty, prerelease: false),
                UrlOrPath: null,
                GithubApiReleasesUrl: api);
        }

        return new Feed(
            Description: raw,
            Source: null,
            UrlOrPath: raw,
            GithubApiReleasesUrl: null);
    }

    internal static string? TryGithubApiFromRepoUrl(string repoUrl)
    {
        // https://github.com/Owner/Repo → api.github.com/repos/Owner/Repo/releases
        if (!Uri.TryCreate(repoUrl.TrimEnd('/'), UriKind.Absolute, out var uri))
            return null;
        if (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return null;
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        return $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases?per_page=10";
    }

    private enum GithubFeedState
    {
        Unknown,
        Empty,
        HasPresence
    }

    private sealed record Feed(
        string Description,
        IUpdateSource? Source,
        string? UrlOrPath,
        string? GithubApiReleasesUrl);
}

public sealed class PresenceUpdateCheckResult
{
    public enum Kind
    {
        UpToDate,
        Available,
        DevBuild,
        FeedEmpty,
        Failed
    }

    public Kind Status { get; init; }
    public string Message { get; init; } = "";
    public string? CurrentVersion { get; init; }
    public string? AvailableVersion { get; init; }
    public UpdateInfo? Update { get; init; }

    public static PresenceUpdateCheckResult UpToDate(string version) => new()
    {
        Status = Kind.UpToDate,
        CurrentVersion = version,
        Message = $"You're on the latest Presence ({version}). Host updates via chrome Update / Update now."
    };

    public static PresenceUpdateCheckResult Available(string current, string remote, UpdateInfo info) => new()
    {
        Status = Kind.Available,
        CurrentVersion = current,
        AvailableVersion = remote,
        Update = info,
        Message = $"Presence update available: {remote} (you have {current}). Chrome Update also rebuilds Host first."
    };

    public static PresenceUpdateCheckResult DevBuild(string version, string message) => new()
    {
        Status = Kind.DevBuild,
        CurrentVersion = version,
        Message = message
    };

    public static PresenceUpdateCheckResult FeedEmpty(string version, string message) => new()
    {
        Status = Kind.FeedEmpty,
        CurrentVersion = version,
        Message = message
    };

    public static PresenceUpdateCheckResult Fail(string message) => new()
    {
        Status = Kind.Failed,
        Message = message
    };
}

public sealed class PresenceUpdateApplyResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";

    public static PresenceUpdateApplyResult Restarting() => new()
    {
        Ok = true,
        Message = "Installing update and restarting…"
    };

    public static PresenceUpdateApplyResult Fail(string message) => new()
    {
        Ok = false,
        Message = message
    };
}
