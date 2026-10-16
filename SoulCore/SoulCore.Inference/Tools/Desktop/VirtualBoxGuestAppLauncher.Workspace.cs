using System.Globalization;
using System.Text;
using SoulCore.Core;

namespace SoulCore.Inference.Tools.Desktop;

public sealed partial class VirtualBoxGuestAppLauncher
{
    private const string ListScript =
        """
        import os, sys
        p, home = sys.argv[1], sys.argv[2]
        real = os.path.realpath(p)
        if real != home and not real.startswith(home + "/"):
            sys.stderr.write("path escapes guest home\n")
            sys.exit(2)
        if not os.path.isdir(real):
            sys.stderr.write("not a directory\n")
            sys.exit(3)
        names = sorted(os.listdir(real))
        if not names:
            print("(empty directory)")
        else:
            for name in names:
                kind = "dir " if os.path.isdir(os.path.join(real, name)) else "file"
                print(f"{kind} {name}")
        """;

    private const string ReadScript =
        """
        import os, sys
        p, home, limit = sys.argv[1], sys.argv[2], int(sys.argv[3])
        real = os.path.realpath(p)
        if real != home and not real.startswith(home + "/"):
            sys.stderr.write("path escapes guest home\n")
            sys.exit(2)
        if not os.path.isfile(real):
            sys.stderr.write("not a file\n")
            sys.exit(3)
        data = open(real, "rb").read(limit + 1)
        if b"\0" in data[:8192]:
            sys.stderr.write("not a text file\n")
            sys.exit(4)
        text = data.decode("utf-8", "replace")
        if len(data) > limit:
            sys.stdout.write(text[:limit] + "\n[truncated]")
        else:
            sys.stdout.write(text)
        """;

    private const string PrepareWriteScript =
        """
        import os, sys
        parent, home, dest = sys.argv[1], sys.argv[2], sys.argv[3]
        cur = parent
        while not os.path.exists(cur):
            nxt = os.path.dirname(cur)
            if nxt == cur:
                break
            cur = nxt
        real = os.path.realpath(cur)
        if real != home and not real.startswith(home + "/"):
            sys.stderr.write("path escapes guest home\n")
            sys.exit(2)
        if os.path.lexists(dest):
            landed = os.path.realpath(dest)
            if landed != home and not landed.startswith(home + "/"):
                sys.stderr.write("path escapes guest home\n")
                sys.exit(2)
        os.makedirs(parent, exist_ok=True)
        """;

    public string Home
    {
        get
        {
            GuestPathGuard.TryHome(_username(), out var home, out _);
            return string.IsNullOrEmpty(home) ? "/home/victoria" : home;
        }
    }

    public async Task<DesktopOpResult> ListAsync(string path, CancellationToken ct = default)
    {
        if (!TryGuestPath(path, allowEmptyAsHome: true, out var resolved, out var error))
            return new DesktopOpResult(false, error, null);

        var ran = await GuestRunTextAsync(
                "/usr/bin/python3",
                new[] { "-c", ListScript, resolved, Home },
                timeoutMs: 20000,
                ct)
            .ConfigureAwait(false);
        return ran;
    }

    public async Task<DesktopOpResult> ReadAsync(string path, CancellationToken ct = default)
    {
        if (!TryGuestPath(path, allowEmptyAsHome: false, out var resolved, out var error))
            return new DesktopOpResult(false, error, null);

        return await GuestRunTextAsync(
                "/usr/bin/python3",
                new[] { "-c", ReadScript, resolved, Home, GuestPathGuard.MaxReadChars.ToString(CultureInfo.InvariantCulture) },
                timeoutMs: 20000,
                ct)
            .ConfigureAwait(false);
    }

    public async Task<DesktopOpResult> WriteAsync(string path, string content, CancellationToken ct = default)
    {
        if (!TryGuestPath(path, allowEmptyAsHome: false, out var resolved, out var error))
            return new DesktopOpResult(false, error, null);
        content ??= "";
        if (content.Length > GuestPathGuard.MaxWriteChars)
            return new DesktopOpResult(false, "guest file is too large to write in one call", null);

        var parent = resolved.Contains('/')
            ? resolved[..resolved.LastIndexOf('/')]
            : Home;
        if (parent.Length == 0)
            parent = Home;

        var prepared = await GuestRunTextAsync(
                "/usr/bin/python3",
                new[] { "-c", PrepareWriteScript, parent, Home, resolved },
                timeoutMs: 20000,
                ct)
            .ConfigureAwait(false);
        if (!prepared.Success)
            return prepared;

        var temp = Path.Combine(Path.GetTempPath(), "hv-guest-write-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(temp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct)
                .ConfigureAwait(false);
            var copied = await GuestCopyFileAsync(temp, resolved, ct).ConfigureAwait(false);
            if (!copied.Success)
                return copied;
            return new DesktopOpResult(
                true,
                $"wrote {content.Length} chars to {resolved}",
                new { path = resolved, chars = content.Length });
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch
            {
                // best-effort
            }
        }
    }

    public async Task<DesktopOpResult> ShellAsync(
        string command,
        string? cwd,
        int timeoutSeconds,
        CancellationToken ct = default,
        bool allowElevatedShell = false)
    {
        if (!GuestPathGuard.TryValidateCommand(command, out var commandError, allowElevatedShell))
            return new DesktopOpResult(false, commandError, null);
        if (!GuestPathGuard.TryHome(_username(), out var home, out var homeError))
            return new DesktopOpResult(false, homeError, null);

        var cwdRaw = string.IsNullOrWhiteSpace(cwd) ? home : cwd;
        if (!GuestPathGuard.TryResolve(cwdRaw, home, out var resolvedCwd, out var cwdError))
            return new DesktopOpResult(false, cwdError, null);

        var seconds = timeoutSeconds <= 0
            ? GuestPathGuard.DefaultShellTimeoutSeconds
            : Math.Clamp(timeoutSeconds, 1, GuestPathGuard.MaxShellTimeoutSeconds);
        var script =
            "export LANG=C.UTF-8\n" +
            "export LC_ALL=C.UTF-8\n" +
            "export PYTHONIOENCODING=utf-8\n" +
            "export PYTHONUTF8=1\n" +
            "export PATH=\"$HOME/.local/node/bin:$PATH\"\n" +
            "home=" + GuestPathGuard.BashSingleQuote(home) + "\n" +
            "req=" + GuestPathGuard.BashSingleQuote(resolvedCwd) + "\n" +
            "real=$(readlink -f -- \"$req\") || exit 2\n" +
            "case \"$real\" in\n" +
            "\"$home\"|\"$home\"/*) cd -- \"$real\" || exit 1 ;;\n" +
            "*) echo 'path escapes guest home' >&2; exit 2 ;;\n" +
            "esac\n" +
            command.Trim() + "\n";

        var ran = await GuestRunTextAsync(
                "/bin/bash",
                new[] { "-lc", script },
                timeoutMs: seconds * 1000,
                ct)
            .ConfigureAwait(false);
        if (!ran.Success)
            return ran;

        var text = ran.Content ?? "";
        if (text.Length > GuestPathGuard.MaxShellOutputChars)
            text = text[..GuestPathGuard.MaxShellOutputChars] + "\n[truncated]";
        if (text.Length == 0)
            text = "(no output)";
        return new DesktopOpResult(true, text, new { cwd = resolvedCwd, timeoutSeconds = seconds });
    }

    private bool TryGuestPath(string? path, bool allowEmptyAsHome, out string resolved, out string error)
    {
        if (!GuestPathGuard.TryHome(_username(), out var home, out error))
        {
            resolved = "";
            return false;
        }

        if (allowEmptyAsHome && string.IsNullOrWhiteSpace(path))
        {
            resolved = home;
            error = "";
            return true;
        }

        return GuestPathGuard.TryResolve(path, home, out resolved, out error);
    }

    private async Task<DesktopOpResult> GuestCopyFileAsync(string hostFile, string guestPath, CancellationToken ct)
    {
        var auth = RequirePassword();
        if (auth.Error is not null)
            return auth.Error;

        using var passFile = new PasswordFile(auth.Password!);
        var argv = new List<string>
        {
            "guestcontrol", _vmName, "copyto",
            "--username", auth.User,
            "--passwordfile", passFile.Path,
            "--quiet",
            hostFile,
            guestPath
        };
        var (exit, stdout, stderr) = await RunGuestControlAsync(argv, ct).ConfigureAwait(false);
        if (exit != 0)
        {
            return new DesktopOpResult(
                false,
                $"guestcontrol copyto failed (exit {exit}): {Redact(JoinOut(stdout, stderr), auth.Password)}",
                null);
        }

        return new DesktopOpResult(true, "copied", null);
    }

    private async Task<DesktopOpResult> GuestRunTextAsync(
        string exe, IReadOnlyList<string> exeArgs, int timeoutMs, CancellationToken ct)
    {
        var auth = RequirePassword();
        if (auth.Error is not null)
            return auth.Error;

        var putenv = await SessionPutenvAsync(auth.User, auth.Password!, ct).ConfigureAwait(false);
        using var passFile = new PasswordFile(auth.Password!);
        var argv = BuildGuestControl(
            _vmName,
            "run",
            auth.User,
            passFile.Path,
            exe,
            putenv,
            exeArgs,
            waitOutput: true,
            timeoutMs: timeoutMs);
        var (exit, stdout, stderr) = await RunGuestControlAsync(argv, ct).ConfigureAwait(false);
        var text = CombineGuestText(stdout, stderr);
        if (exit != 0)
        {
            return new DesktopOpResult(
                false,
                $"guestcontrol run '{exe}' failed (exit {exit}): {Redact(text, auth.Password)}",
                null);
        }

        return new DesktopOpResult(true, text, null);
    }

    private static string CombineGuestText(string? stdout, string? stderr)
    {
        var text = Utf8TextRepair.Repair(stdout ?? "");
        var err = Utf8TextRepair.Repair((stderr ?? "").Trim());
        if (err.Length == 0)
            return text;
        return text.Length == 0 ? err : text + "\n" + err;
    }
}
