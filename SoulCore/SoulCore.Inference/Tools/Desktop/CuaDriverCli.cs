using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SoulCore.Inference.Tools.Desktop;

/// <summary>
/// Thin CLI client for local <c>cua-driver</c> (same stack LLMOD/Hermes used for
/// the large tinted agent cursor that never moves the OS mouse).
/// </summary>
public sealed class CuaDriverCli
{
    public const string DefaultSessionId = "soulcore-victoria";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _exePath;
    private readonly TimeSpan _timeout;

    public CuaDriverCli(string exePath, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            throw new ArgumentException("cua-driver path required", nameof(exePath));
        _exePath = exePath;
        _timeout = timeout ?? TimeSpan.FromSeconds(45);
    }

    public string ExePath => _exePath;

    public static string? TryFindExe()
    {
        var configured = Environment.GetEnvironmentVariable("SOULCORE_CUA_DRIVER");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Cua", "cua-driver", "bin", "cua-driver.exe");
        if (File.Exists(local))
            return local;

        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), "cua-driver.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // ignore PATH scan failures
        }

        return null;
    }

    public static bool IsAvailable() => TryFindExe() is not null;

    public async Task<CuaCallResult> CallAsync(
        string tool,
        object args,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        var json = JsonSerializer.Serialize(args, JsonOpts);

        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                ArgumentList = { "call", tool },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        try
        {
            if (!proc.Start())
                return CuaCallResult.Fail("failed to start cua-driver process");

            await proc.StandardInput.WriteAsync(json.AsMemory(), ct).ConfigureAwait(false);
            await proc.StandardInput.FlushAsync(ct).ConfigureAwait(false);
            proc.StandardInput.Close();

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return CuaCallResult.Fail($"cua-driver call '{tool}' timed out after {_timeout.TotalSeconds:0}s");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (proc.ExitCode != 0)
            {
                var err = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return CuaCallResult.Fail(string.IsNullOrWhiteSpace(err)
                    ? $"cua-driver call '{tool}' exited {proc.ExitCode}"
                    : err.Trim());
            }

            return CuaCallResult.Ok(stdout?.Trim() ?? "", stderr?.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CuaCallResult.Fail($"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

public sealed record CuaCallResult(bool Success, string Stdout, string? Error)
{
    public static CuaCallResult Ok(string stdout, string? stderr = null) =>
        new(true, stdout, string.IsNullOrWhiteSpace(stderr) ? null : stderr);

    public static CuaCallResult Fail(string error) =>
        new(false, "", error);

    public bool TryParseJson(out JsonElement element)
    {
        element = default;
        if (string.IsNullOrWhiteSpace(Stdout))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(Stdout);
            element = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
