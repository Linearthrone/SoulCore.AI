using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Protocol.Tests;

public sealed class GuestWorkspaceTests
{
    [Fact]
    public void Relative_path_stays_under_home()
    {
        Assert.True(GuestPathGuard.TryResolve("booklist/src/App.jsx", "/home/victoria", out var resolved, out var error), error);
        Assert.Equal("/home/victoria/booklist/src/App.jsx", resolved);
    }

    [Fact]
    public void Absolute_escape_is_refused()
    {
        Assert.False(GuestPathGuard.TryResolve("/etc/passwd", "/home/victoria", out _, out var error));
        Assert.Contains("escapes", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotdot_escape_is_refused()
    {
        Assert.False(GuestPathGuard.TryResolve("booklist/../../etc/passwd", "/home/victoria", out _, out var error));
        Assert.Contains("escapes", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Sudo_is_refused_by_default()
    {
        Assert.False(GuestPathGuard.TryValidateCommand("sudo systemctl restart nginx", out var error));
        Assert.Contains("sudo", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Sudo_is_allowed_when_elevated_shell_enabled()
    {
        Assert.True(GuestPathGuard.TryValidateCommand(
            "sudo systemctl restart nginx", out _, allowElevatedShell: true));
    }

    [Fact]
    public void Package_install_stays_refused_even_with_elevated_shell()
    {
        Assert.False(GuestPathGuard.TryValidateCommand(
            "sudo apt-get install nodejs", out var error, allowElevatedShell: true));
        Assert.Contains("package install", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Shell_sudo_does_not_call_vbox_when_elevated_off()
    {
        var calls = new List<string>();
        var launcher = Launcher(calls, (_, _) => (0, "", ""));
        var result = await launcher.ShellAsync("sudo systemctl status", null, 30);
        Assert.False(result.Success);
        Assert.Contains("sudo", result.Content, StringComparison.Ordinal);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Read_refuses_path_outside_home_without_vbox()
    {
        var calls = new List<string>();
        var launcher = Launcher(calls, (_, _) => (0, "", ""));
        var result = await launcher.ReadAsync("/etc/shadow");
        Assert.False(result.Success);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Write_copies_into_guest_home()
    {
        var calls = new List<string>();
        var launcher = Launcher(calls, (argv, _) =>
        {
            var joined = string.Join('\n', argv);
            if (joined.Contains("/usr/bin/id", StringComparison.Ordinal))
                return (0, "1000\n", "");
            if (joined.Contains("mutter", StringComparison.Ordinal) || joined.Contains(".Xauthority", StringComparison.Ordinal))
                return (0, "/home/victoria/.Xauthority\n", "");
            if (joined.Contains("copyto", StringComparison.Ordinal))
                return (0, "", "");
            return (0, "", "");
        });

        var result = await launcher.WriteAsync("booklist/src/App.jsx", "export default function App(){return null}\n");
        Assert.True(result.Success, result.Content);
        Assert.Contains("/home/victoria/booklist/src/App.jsx", result.Content, StringComparison.Ordinal);
        Assert.Contains(calls, c => c.Contains("copyto", StringComparison.Ordinal) && c.Contains("/home/victoria/booklist/src/App.jsx", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, c => c.Contains("guest-pass", StringComparison.Ordinal));
    }

    [Fact]
    public void VsCode_file_goto_reuses_the_window()
    {
        Assert.True(VsCodeLaunch.TryPlan(
            "/usr/bin/code",
            "/home/victoria/booklist/src/App.jsx",
            directory: false,
            line: 40,
            column: 3,
            out var args,
            out var error), error);
        Assert.Equal(new[] { "-r", "-g", "/home/victoria/booklist/src/App.jsx:40:3" }, args);
    }

    [Fact]
    public void VsCode_folder_does_not_take_a_line()
    {
        Assert.False(VsCodeLaunch.TryPlan(
            "/usr/bin/codium",
            "/home/victoria/booklist",
            directory: true,
            line: 1,
            column: null,
            out _,
            out var error));
        Assert.Contains("directory", error, StringComparison.Ordinal);
    }

    [Fact]
    public void VsCode_rejects_a_caller_supplied_binary()
    {
        Assert.False(VsCodeLaunch.TryPlan(
            "/tmp/code",
            "/home/victoria/booklist",
            directory: true,
            line: null,
            column: null,
            out _,
            out var error));
        Assert.Contains("allowlisted", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenInEditor_starts_code_at_the_line()
    {
        var calls = new List<string>();
        var launcher = Launcher(calls, (argv, _) =>
        {
            var joined = string.Join('\n', argv);
            if (joined.Contains("hv-vscode-probe", StringComparison.Ordinal))
            {
                return (0, "/usr/bin/code\nfile\n/home/victoria/booklist/src/App.jsx\n", "");
            }

            return (0, "", "");
        });

        var result = await launcher.OpenInEditorAsync("booklist/src/App.jsx", 40, 3);
        Assert.True(result.Success, result.Content);
        Assert.Contains("line 40", result.Content, StringComparison.Ordinal);
        Assert.Contains(
            calls,
            c => c.Contains("start", StringComparison.Ordinal)
                 && c.Contains("/usr/bin/code", StringComparison.Ordinal)
                 && c.Contains("/home/victoria/booklist/src/App.jsx:40:3", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, c => c.Contains("guest-pass", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenInEditor_refuses_a_path_outside_home_without_vbox()
    {
        var calls = new List<string>();
        var launcher = Launcher(calls, (_, _) => (0, "", ""));
        var result = await launcher.OpenInEditorAsync("/etc/passwd", null, null);
        Assert.False(result.Success);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task OpenInEditor_does_not_start_when_the_editor_is_missing()
    {
        var calls = new List<string>();
        var launcher = Launcher(calls, (argv, _) =>
        {
            var joined = string.Join('\n', argv);
            if (joined.Contains("hv-vscode-probe", StringComparison.Ordinal))
                return (4, "", "no VS Code or VSCodium binary");
            return (0, "", "");
        });

        var result = await launcher.OpenInEditorAsync("booklist", null, null);
        Assert.False(result.Success);
        Assert.Contains("no VS Code", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(calls, c => c.Contains("\nstart\n", StringComparison.Ordinal) || c.Contains("guestcontrol\nKsDevice\nstart", StringComparison.Ordinal));
    }

    private static VirtualBoxGuestAppLauncher Launcher(
        List<string> calls,
        Func<IReadOnlyList<string>, CancellationToken, (int Exit, string Stdout, string Stderr)> handle)
    {
        return new VirtualBoxGuestAppLauncher(
            "KsDevice",
            "VBoxManage",
            (exe, argv, ct) =>
            {
                calls.Add(string.Join('\n', argv));
                return Task.FromResult(handle(argv, ct));
            },
            password: () => "guest-pass",
            username: () => "victoria");
    }
}
