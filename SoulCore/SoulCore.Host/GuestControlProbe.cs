using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Host;

/// <summary>
/// Evidence CLI: prove VirtualBox guestcontrol can log on as the Ubuntu guest
/// user from <c>SOULCORE_VBOX_GUEST_*</c> (never prints the password).
/// </summary>
internal static class GuestControlProbe
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        var vm = ResolveVmName(args);
        var user = VirtualBoxGuestAppLauncher.ResolveGuestUser();
        var passPresent = !string.IsNullOrWhiteSpace(VirtualBoxGuestAppLauncher.ResolveGuestPassword());

        Console.WriteLine($"vm={vm}");
        Console.WriteLine($"user={user}");
        Console.WriteLine($"SOULCORE_VBOX_GUEST_PASS: present={passPresent}");

        if (!passPresent)
        {
            Console.WriteLine(
                "fail: set SOULCORE_VBOX_GUEST_PASS in SoulCore/.env (Ubuntu password for that user), then retry.");
            return 1;
        }

        var launcher = new VirtualBoxGuestAppLauncher(vm);
        // whoami with NO duplicated exe after "--". VBoxManage sets argv[0] from
        // --exe; passing /usr/bin/id again makes GNU id treat it as a username
        // ("no such user") even when logon succeeded.
        var result = await launcher.ProbeWhoamiAsync(ct).ConfigureAwait(false);
        if (!result.Success)
        {
            Console.WriteLine("fail: " + result.Content);
            Console.WriteLine(
                "hint: password must be the Ubuntu login for this user; Guest Additions must be running; " +
                "desktop session should be logged in. Do not pass the exe path again after --.");
            return 2;
        }

        var who = (result.Content ?? "").Trim();
        Console.WriteLine($"whoami={who}");
        if (!string.Equals(who, user, StringComparison.Ordinal))
        {
            Console.WriteLine($"warn: expected user '{user}' but guest reported '{who}'.");
            return 3;
        }

        Console.WriteLine("ok: guestcontrol logon works");
        return 0;
    }

    private static string ResolveVmName(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--vm", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                return args[i + 1].Trim();
            }
        }

        var fromEnv = Environment.GetEnvironmentVariable("SOULCORE_Tools__DesktopTargetWindowTitle");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        return "victoria-sandbox";
    }
}
