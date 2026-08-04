using System.Diagnostics;
using System.Net.Http;

namespace House.ChatDesktop.Services;

/// <summary>
/// Loopback-only local stack control: Host / Hermes scripts + Ollama / Comfy probes.
/// Never targets non-127.0.0.1.
/// </summary>
public sealed class LocalStackControl : IDisposable
{
    public const string HermesHealthUrl = "http://127.0.0.1:8642/health";
    public const string OllamaTagsUrl = "http://127.0.0.1:11434/api/tags";
    public const string ComfyUiUrl = "http://127.0.0.1:8188/system_stats";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public string? RepoRoot { get; } = FindRepoRoot();

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

    public Task<bool> ProbeHermesAsync(CancellationToken ct = default) =>
        ProbeUrlAsync(HermesHealthUrl, ct);

    public Task<bool> ProbeOllamaAsync(CancellationToken ct = default) =>
        ProbeUrlAsync(OllamaTagsUrl, ct);

    public Task<bool> ProbeComfyAsync(CancellationToken ct = default) =>
        ProbeUrlAsync(ComfyUiUrl, ct);

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

    public Task<LocalStackActionResult> StartHermesAsync(CancellationToken ct = default) =>
        RunScriptAsync("SoulCore\\scripts\\start-hermes.ps1", Array.Empty<string>(), ct);

    public Task<LocalStackActionResult> StopHermesAsync(CancellationToken ct = default) =>
        RunPowerShellInlineAsync(
            @"
$ErrorActionPreference='Continue'
try { hermes gateway stop 2>$null | Out-Null } catch {}
$pidFile = Join-Path (Get-Location) 'SoulCore\scripts\.hermes.pid'
if (Test-Path $pidFile) {
  $p = 0; try { $p = [int](Get-Content $pidFile | Select-Object -First 1) } catch {}
  if ($p -gt 0) { Stop-Process -Id $p -Force -ErrorAction SilentlyContinue }
  Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}
Get-NetTCPConnection -LocalPort 8642 -State Listen -ErrorAction SilentlyContinue |
  Where-Object { $_.LocalAddress -eq '127.0.0.1' } |
  ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
'Hermes stop attempted'
",
            ct);

    public async Task<LocalStackActionResult> RestartHermesAsync(CancellationToken ct = default)
    {
        var stop = await StopHermesAsync(ct).ConfigureAwait(false);
        var start = await StartHermesAsync(ct).ConfigureAwait(false);
        return new LocalStackActionResult(
            start.Ok,
            $"stop: {stop.Detail}; start: {start.Detail}");
    }

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

    private Task<LocalStackActionResult> RunScriptAsync(
        string relativeScript,
        IReadOnlyList<string> extraArgs,
        CancellationToken ct,
        bool wait = true)
    {
        if (RepoRoot is null)
            return Task.FromResult(LocalStackActionResult.Fail("repo root not found (SoulCore/.env or ALLSTART.ps1)"));

        var script = Path.Combine(RepoRoot, relativeScript);
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
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
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

    private static string TrimDetail(string raw)
    {
        var t = raw.Trim();
        if (t.Length <= 400)
            return t;
        return t[^400..];
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            var allstart = Path.Combine(dir.FullName, "ALLSTART.ps1");
            var soulEnv = Path.Combine(dir.FullName, "SoulCore", ".env");
            if (File.Exists(allstart) || File.Exists(soulEnv))
                return dir.FullName;
        }

        return null;
    }

    public void Dispose() => _http.Dispose();
}

public readonly record struct LocalStackActionResult(bool Ok, string Detail)
{
    public static LocalStackActionResult Succeed(string detail) => new(true, detail);
    public static LocalStackActionResult Fail(string detail) => new(false, detail);
}
