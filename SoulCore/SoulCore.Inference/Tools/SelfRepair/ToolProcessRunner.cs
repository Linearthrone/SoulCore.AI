using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace SoulCore.Inference.Tools.SelfRepair;

public sealed record ToolProcessResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);

/// <summary>
/// Starts a child process with redirected output. Used by the tool sandbox so a
/// bad fix cannot take down the host.
/// </summary>
public interface IToolProcessRunner
{
    Task<ToolProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool scrubSecrets = false);
}

public sealed class ToolProcessRunner : IToolProcessRunner
{
    private const int MaxStreamChars = 20_000;

    public async Task<ToolProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool scrubSecrets = false)
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
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);
        // A sandbox build must not join the MSBuild node that is already
        // compiling or hosting SoulCore, or it will try to overwrite locked bins.
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        psi.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        if (scrubSecrets)
            RemoveSensitiveEnvironment(psi);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => AppendCapped(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => AppendCapped(stderr, e.Data);

        try
        {
            if (!process.Start())
                return new ToolProcessResult(-1, "", "failed to start process", false);
        }
        catch (Win32Exception ex)
        {
            return new ToolProcessResult(-1, "", ex.Message, false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (!cancellationToken.IsCancellationRequested)
                return new ToolProcessResult(-1, stdout.ToString(), stderr.ToString(), TimedOut: true);
            throw;
        }

        process.WaitForExit();
        return new ToolProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString(), false);
    }

    internal static void RemoveSensitiveEnvironment(ProcessStartInfo psi)
    {
        var names = new List<string>();
        foreach (var key in psi.Environment.Keys)
        {
            if (IsSensitiveEnvName(key))
                names.Add(key);
        }
        foreach (var name in names)
            psi.Environment.Remove(name);
    }

    public static bool IsSensitiveEnvName(string name)
    {
        var n = name.ToUpperInvariant();
        return n.Contains("KEY", StringComparison.Ordinal)
            || n.Contains("SECRET", StringComparison.Ordinal)
            || n.Contains("TOKEN", StringComparison.Ordinal)
            || n.Contains("PASSWORD", StringComparison.Ordinal)
            || n.Contains("PWD", StringComparison.Ordinal)
            || n.Contains("CREDENTIAL", StringComparison.Ordinal)
            || n.Contains("CONNECTION", StringComparison.Ordinal);
    }

    private static void AppendCapped(StringBuilder buffer, string? line)
    {
        if (line is null || buffer.Length >= MaxStreamChars)
            return;
        var room = MaxStreamChars - buffer.Length;
        if (line.Length > room)
            buffer.Append(line.AsSpan(0, room));
        else
            buffer.AppendLine(line);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // already gone
        }

        try
        {
            process.WaitForExit(2_000);
        }
        catch (Exception)
        {
            // best effort
        }
    }
}
