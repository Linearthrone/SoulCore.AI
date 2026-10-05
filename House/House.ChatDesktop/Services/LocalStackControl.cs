using System.Diagnostics;
using System.Net.Http;

namespace House.ChatDesktop.Services;

/// <summary>
/// Loopback-only local stack control: Host scripts + Ollama / Comfy probes.
/// Never targets non-127.0.0.1.
/// </summary>
public sealed class LocalStackControl : IDisposable
{
    public const string OllamaTagsUrl = "http://127.0.0.1:11434/api/tags";
    public const string ComfyUiUrl = "http://127.0.0.1:8188/system_stats";
    public const string DefaultHostHealthUrl = "http://127.0.0.1:7700/health";

    /// <summary>Env override for repo root when Presence is installed outside the checkout.</summary>
    public const string RepoRootEnvName = "HOUSE_SOULCORE_REPO";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private string? _repoRoot;

    public LocalStackControl(string? configuredRepoRoot = null)
    {
        _repoRoot = ResolveRepoRoot(configuredRepoRoot);
    }

    public string? RepoRoot => _repoRoot;

    /// <summary>
    /// Update persisted/config root and re-resolve (e.g. after Settings save).
    /// </summary>
    public void SetConfiguredRepoRoot(string? configuredRepoRoot) =>
        _repoRoot = ResolveRepoRoot(configuredRepoRoot);

    public async Task<bool> ProbeUrlAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> ProbeOllamaAsync(CancellationToken ct = default) =>
        ProbeUrlAsync(OllamaTagsUrl, ct);

    public Task<bool> ProbeComfyAsync(CancellationToken ct = default) =>
        ProbeUrlAsync(ComfyUiUrl, ct);

    public Task<bool> ProbeHostHealthAsync(CancellationToken ct = default)
    {
        var url = $"http://{ConnectionDefaults.Host}:{ConnectionDefaults.Port}/health";
        return ProbeUrlAsync(url, ct);
    }

    /// <summary>
    /// PROP-4: victoria-sandbox running? Loopback/local VBoxManage only.
    /// </summary>
    public async Task<bool> ProbeSandboxAsync(CancellationToken ct = default)
    {
        try
        {
            var vbox = FindVBoxManage();
            if (vbox is null)
                return false;

            var psi = new ProcessStartInfo
            {
                FileName = vbox,
                ArgumentList = { "list", "runningvms" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return stdout.Contains("victoria-sandbox", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindVBoxManage()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Oracle\VirtualBox\VBoxManage.exe",
            @"C:\Program Files\Oracle\VirtualBox\VBoxManage",
            "/usr/bin/VBoxManage",
            "VBoxManage"
        };
        foreach (var c in candidates)
        {
            if (c is "VBoxManage")
                return c;
            if (File.Exists(c))
                return c;
        }

        return null;
    }

    public Task<LocalStackActionResult> StartHostAsync(CancellationToken ct = default) =>
        RunScriptAsync("SoulCore\\scripts\\start-soulcore.ps1", Array.Empty<string>(), ct);

    public Task<LocalStackActionResult> StopHostAsync(CancellationToken ct = default) =>
        RunScriptAsync("SoulCore\\scripts\\stop-soulcore.ps1", Array.Empty<string>(), ct);

    public async Task<LocalStackActionResult> RestartHostAsync(CancellationToken ct = default)
    {
        var stop = await StopHostAsync(ct).ConfigureAwait(false);
        var start = await StartHostAsync(ct).ConfigureAwait(false);
        return new LocalStackActionResult(
            start.Ok,
            $"stop: {stop.Detail}; start: {start.Detail}");
    }

    public Task<LocalStackActionResult> StartAllAsync(CancellationToken ct = default) =>
        RunScriptAsync("ALLSTART.ps1", Array.Empty<string>(), ct);

    public Task<LocalStackActionResult> StartOllamaAsync(CancellationToken ct = default) =>
        RunPowerShellInlineAsync(
            @"
$ErrorActionPreference='Stop'
try {
  $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/tags' -UseBasicParsing -TimeoutSec 2
  if ($r.StatusCode -eq 200) { 'Ollama already up'; exit 0 }
} catch {}
$ollama = Get-Command ollama -ErrorAction SilentlyContinue
if (-not $ollama) { throw 'ollama not on PATH' }
Start-Process -FilePath $ollama.Source -ArgumentList @('serve') -WindowStyle Hidden
Start-Sleep -Seconds 2
$r2 = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/tags' -UseBasicParsing -TimeoutSec 5
'Ollama serve started'
",
            ct);

    public Task<LocalStackActionResult> RestartChatDesktopAsync(CancellationToken ct = default) =>
        RunScriptAsync("start-desktopgui.ps1", Array.Empty<string>(), ct, wait: false);

    /// <summary>PROP-16.2 relative path from repo root (Presence spawns detached).</summary>
    public const string RestartStackRelativeScript = "House\\scripts\\restart-stack.ps1";

    /// <summary>
    /// PROP-16: git fetch + ahead/behind vs upstream + dirty porcelain.
    /// </summary>
    public async Task<LocalStackGitStatus> GetGitStatusAsync(CancellationToken ct = default)
    {
        if (RepoRoot is null)
        {
            return LocalStackGitStatus.Unavailable(
                "SoulCore repo not found. Set HOUSE_SOULCORE_REPO or Presence Settings → SoulCore repo folder.");
        }

        var fetch = await RunGitAsync(new[] { "fetch", "--quiet" }, ct).ConfigureAwait(false);
        if (!fetch.Ok)
        {
            return LocalStackGitStatus.Unavailable($"git fetch failed: {fetch.Detail}");
        }

        var upstream = await RunGitAsync(
            new[] { "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}" },
            ct).ConfigureAwait(false);
        var upstreamName = upstream.Ok ? upstream.Detail.Trim() : null;

        var behind = 0;
        var ahead = 0;
        if (upstream.Ok)
        {
            var counts = await RunGitAsync(
                new[] { "rev-list", "--left-right", "--count", "@{u}...HEAD" },
                ct).ConfigureAwait(false);
            if (counts.Ok && TryParseRevListLeftRight(counts.Detail, out behind, out ahead))
            {
                // ok
            }
            else if (!counts.Ok)
            {
                return LocalStackGitStatus.Unavailable($"git rev-list failed: {counts.Detail}");
            }
        }

        var porcelain = await RunGitAsync(new[] { "status", "--porcelain" }, ct).ConfigureAwait(false);
        if (!porcelain.Ok)
            return LocalStackGitStatus.Unavailable($"git status failed: {porcelain.Detail}");

        var dirty = IsPorcelainDirty(porcelain.Detail);
        var detail = BuildGitStatusDetail(behind, ahead, dirty, upstreamName);
        return new LocalStackGitStatus(true, behind, ahead, dirty, upstreamName, detail);
    }

    /// <summary>PROP-16: fast-forward only pull. Never force / hard-reset.</summary>
    public async Task<LocalStackActionResult> PullAsync(CancellationToken ct = default)
    {
        if (RepoRoot is null)
            return LocalStackActionResult.Fail(
                "repo root not found — set HOUSE_SOULCORE_REPO or Settings → SoulCore repo folder");

        var pull = await RunGitAsync(new[] { "pull", "--ff-only" }, ct).ConfigureAwait(false);
        if (!pull.Ok)
        {
            return LocalStackActionResult.Fail(
                $"git pull --ff-only failed (no force, no hard reset): {pull.Detail}");
        }

        return LocalStackActionResult.Succeed(
            string.IsNullOrWhiteSpace(pull.Detail) ? "git pull --ff-only ok" : pull.Detail);
    }

    /// <summary>
    /// PROP-16: rebuild + restart Host via start-soulcore.ps1, then poll /health.
    /// </summary>
    public async Task<LocalStackActionResult> UpdateHostAsync(
        CancellationToken ct = default,
        int hostReadyTimeoutSec = 120,
        IProgress<string>? progress = null)
    {
        if (RepoRoot is null)
            return LocalStackActionResult.Fail(
                "repo root not found — set HOUSE_SOULCORE_REPO or Settings → SoulCore repo folder");

        progress?.Report("Rebuilding and restarting Host…");
        var rebuild = await RunScriptAsync(
            "SoulCore\\scripts\\start-soulcore.ps1",
            new[] { "-ForceRebuild", "-RestartHost" },
            ct,
            wait: true).ConfigureAwait(false);
        if (!rebuild.Ok)
            return LocalStackActionResult.Fail($"Host rebuild failed: {rebuild.Detail}");

        var timeout = Math.Clamp(hostReadyTimeoutSec, 15, 300);
        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await ProbeHostHealthAsync(ct).ConfigureAwait(false))
            {
                progress?.Report("Host healthy");
                return LocalStackActionResult.Succeed(
                    string.IsNullOrWhiteSpace(rebuild.Detail)
                        ? "Host rebuilt and healthy"
                        : rebuild.Detail);
            }

            progress?.Report("Waiting for Host /health…");
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        return LocalStackActionResult.Fail(
            $"Host rebuild ran but /health did not answer within {timeout}s. Detail: {rebuild.Detail}");
    }

    /// <summary>
    /// PROP-16: spawn restart-stack.ps1 detached (ALLSTOP kills Presence — must not await).
    /// </summary>
    public Task<LocalStackActionResult> RestartStackAsync(CancellationToken ct = default) =>
        RunScriptAsync(RestartStackRelativeScript, Array.Empty<string>(), ct, wait: false);

    /// <summary>Parse <c>git rev-list --left-right --count A...B</c> stdout (<c>left right</c>).</summary>
    public static bool TryParseRevListLeftRight(string? stdout, out int left, out int right)
    {
        left = 0;
        right = 0;
        if (string.IsNullOrWhiteSpace(stdout))
            return false;
        var parts = stdout.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;
        return int.TryParse(parts[0], out left) && int.TryParse(parts[1], out right);
    }

    public static bool IsPorcelainDirty(string? porcelainStdout) =>
        !string.IsNullOrWhiteSpace(porcelainStdout);

    public static string BuildGitStatusDetail(int behind, int ahead, bool dirty, string? upstream)
    {
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(upstream))
            bits.Add($"upstream {upstream}");
        if (behind == 0 && ahead == 0)
            bits.Add("up to date");
        else
        {
            if (behind > 0) bits.Add($"behind {behind}");
            if (ahead > 0) bits.Add($"ahead {ahead}");
        }

        bits.Add(dirty ? "working tree dirty" : "clean");
        return string.Join("; ", bits);
    }

    private async Task<LocalStackActionResult> RunGitAsync(IReadOnlyList<string> gitArgs, CancellationToken ct)
    {
        if (RepoRoot is null)
            return LocalStackActionResult.Fail("repo root not found");

        var args = new List<string> { "-C", RepoRoot };
        args.AddRange(gitArgs);
        return await RunProcessAsync("git", args, RepoRoot, ct, wait: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensure chat can work: Ollama (if missing) + SoulCore.Host on loopback.
    /// Does not relaunch Presence / ALLSTART GUI (avoids duplicate windows).
    /// </summary>
    public async Task<LocalStackEnsureResult> EnsureStackForChatAsync(
        CancellationToken ct = default,
        int hostReadyTimeoutSec = 90,
        IProgress<string>? progress = null)
    {
        if (RepoRoot is null)
        {
            return LocalStackEnsureResult.Fail(
                "SoulCore repo not found. Set HOUSE_SOULCORE_REPO or Presence Settings → SoulCore repo folder " +
                "(the folder that contains ALLSTART.ps1), then reopen Presence.");
        }

        progress?.Report($"Repo: {RepoRoot}");

        var ollamaWasUp = await ProbeOllamaAsync(ct).ConfigureAwait(false);
        if (!ollamaWasUp)
        {
            progress?.Report("Starting Ollama…");
            var ollama = await StartOllamaAsync(ct).ConfigureAwait(false);
            if (!ollama.Ok)
            {
                // Host can still start; chat will fail without a model — surface warning but continue.
                progress?.Report($"Ollama: {ollama.Detail}");
            }
            else
            {
                progress?.Report("Ollama up");
            }
        }
        else
        {
            progress?.Report("Ollama already up");
        }

        var hostWasUp = await ProbeHostHealthAsync(ct).ConfigureAwait(false);
        if (hostWasUp)
        {
            progress?.Report("Host already up");
            return LocalStackEnsureResult.Succeed(
                hostStarted: false,
                ollamaStarted: !ollamaWasUp,
                detail: "Host already listening; stack ready.");
        }

        progress?.Report("Starting SoulCore.Host…");
        var start = await StartHostAsync(ct).ConfigureAwait(false);
        if (!start.Ok)
        {
            return LocalStackEnsureResult.Fail($"Host start failed: {start.Detail}");
        }

        var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(hostReadyTimeoutSec, 15, 300));
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await ProbeHostHealthAsync(ct).ConfigureAwait(false))
            {
                progress?.Report("Host healthy");
                return LocalStackEnsureResult.Succeed(
                    hostStarted: true,
                    ollamaStarted: !ollamaWasUp,
                    detail: start.Detail);
            }

            progress?.Report("Waiting for Host /health…");
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        return LocalStackEnsureResult.Fail(
            $"Host start ran but /health did not answer within {hostReadyTimeoutSec}s. Detail: {start.Detail}");
    }

    private Task<LocalStackActionResult> RunScriptAsync(
        string relativeScript,
        IReadOnlyList<string> extraArgs,
        CancellationToken ct,
        bool wait = true)
    {
        if (RepoRoot is null)
            return Task.FromResult(LocalStackActionResult.Fail(
                "repo root not found — set HOUSE_SOULCORE_REPO or Settings → SoulCore repo folder"));

        var normalizedRelative = relativeScript.Replace('\\', Path.DirectorySeparatorChar);
        var script = Path.Combine(RepoRoot, normalizedRelative);
        if (!File.Exists(script))
            return Task.FromResult(LocalStackActionResult.Fail($"missing script: {script}"));

        var args = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", script
        };
        args.AddRange(extraArgs);
        return RunProcessAsync("powershell.exe", args, RepoRoot, ct, wait);
    }

    private Task<LocalStackActionResult> RunPowerShellInlineAsync(string scriptBody, CancellationToken ct)
    {
        if (RepoRoot is null)
            return Task.FromResult(LocalStackActionResult.Fail("repo root not found"));

        // Embed working directory into the script so $PSScriptRoot-style paths work via Set-Location.
        var wrapped = $"Set-Location -LiteralPath '{RepoRoot.Replace("'", "''")}';\n" + scriptBody;
        var args = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-Command", wrapped
        };
        return RunProcessAsync("powershell.exe", args, RepoRoot, ct, wait: true);
    }

    private static async Task<LocalStackActionResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken ct,
        bool wait)
    {
        try
        {
            var resolvedExe = ResolveWindowsExecutable(fileName) ?? fileName;
            var psi = new ProcessStartInfo
            {
                FileName = resolvedExe,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            // Start Menu / Velopack launches often miss User PATH (dotnet).
            EnrichPathForStackTools(psi);
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            using var proc = new Process { StartInfo = psi };
            if (!proc.Start())
                return LocalStackActionResult.Fail("failed to start process");

            if (!wait)
                return LocalStackActionResult.Succeed($"started PID {proc.Id} (detached)");

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            var detail = TrimDetail(string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
            if (proc.ExitCode != 0)
                return LocalStackActionResult.Fail(string.IsNullOrWhiteSpace(detail)
                    ? $"exit {proc.ExitCode}"
                    : detail);
            return LocalStackActionResult.Succeed(string.IsNullOrWhiteSpace(detail) ? "ok" : detail);
        }
        catch (Exception ex)
        {
            return LocalStackActionResult.Fail(ex.Message);
        }
    }

    private static string? ResolveWindowsExecutable(string fileName)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        if (fileName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("powershell", StringComparison.OrdinalIgnoreCase))
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var candidate = Path.Combine(systemRoot, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static void EnrichPathForStackTools(ProcessStartInfo psi)
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            var extras = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0"),
            };
            var prefix = string.Join(Path.PathSeparator, extras.Where(Directory.Exists));
            if (string.IsNullOrEmpty(prefix))
                return;
            psi.Environment["PATH"] = prefix + Path.PathSeparator + path;
        }
        catch
        {
            // best-effort
        }
    }

    private static string TrimDetail(string raw)
    {
        var t = raw.Trim();
        if (t.Length <= 400)
            return t;
        return t[^400..];
    }

    /// <summary>
    /// Resolve checkout that contains ALLSTART.ps1 / SoulCore/.env.
    /// Order: configured path → HOUSE_SOULCORE_REPO → walk from BaseDirectory → common user folders.
    /// </summary>
    public static string? ResolveRepoRoot(string? configuredRepoRoot = null)
    {
        foreach (var candidate in EnumerateRepoRootCandidates(configuredRepoRoot))
        {
            if (LooksLikeRepoRoot(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static IEnumerable<string> EnumerateRepoRootCandidates(string? configuredRepoRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredRepoRoot))
            yield return configuredRepoRoot.Trim().Trim('"');

        var env = Environment.GetEnvironmentVariable(RepoRootEnvName);
        if (!string.IsNullOrWhiteSpace(env))
            yield return env.Trim().Trim('"');

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir is not null; i++, dir = dir.Parent)
            yield return dir.FullName;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            foreach (var name in new[]
                     {
                         "Soul_Core",
                         "SoulCore.AI",
                         "SoulCore",
                         Path.Combine("Documents", "Soul_Core"),
                         Path.Combine("Documents", "SoulCore.AI"),
                         Path.Combine("source", "SoulCore.AI"),
                         Path.Combine("src", "SoulCore.AI"),
                     })
            {
                yield return Path.Combine(profile, name);
            }
        }
    }

    public static bool LooksLikeRepoRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            if (!Directory.Exists(path))
                return false;
            var allstart = Path.Combine(path, "ALLSTART.ps1");
            var soulEnv = Path.Combine(path, "SoulCore", ".env");
            var hostCsproj = Path.Combine(path, "SoulCore", "SoulCore.Host", "SoulCore.Host.csproj");
            return File.Exists(allstart) || File.Exists(soulEnv) || File.Exists(hostCsproj);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}

public readonly record struct LocalStackActionResult(bool Ok, string Detail)
{
    public static LocalStackActionResult Succeed(string detail) => new(true, detail);
    public static LocalStackActionResult Fail(string detail) => new(false, detail);
}

public readonly record struct LocalStackEnsureResult(
    bool Ok,
    bool HostStarted,
    bool OllamaStarted,
    string Detail)
{
    public static LocalStackEnsureResult Succeed(bool hostStarted, bool ollamaStarted, string detail) =>
        new(true, hostStarted, ollamaStarted, detail);

    public static LocalStackEnsureResult Fail(string detail) =>
        new(false, false, false, detail);
}

/// <summary>PROP-16: snapshot of checkout vs upstream after fetch.</summary>
public readonly record struct LocalStackGitStatus(
    bool Ok,
    int Behind,
    int Ahead,
    bool Dirty,
    string? Upstream,
    string Detail)
{
    public static LocalStackGitStatus Unavailable(string detail) =>
        new(false, 0, 0, false, null, detail);
}
