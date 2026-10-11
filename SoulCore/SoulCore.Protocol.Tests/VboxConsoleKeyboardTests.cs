using SoulCore.Inference.Tools.Desktop;
using Xunit;

namespace SoulCore.Protocol.Tests;

public class VboxConsoleKeyboardTests
{
    private static readonly string[] Clear = { "aa", "b6", "9d", "b8", "e0", "db" };

    [Fact]
    public void PlanText_LowerA_ReleasesModifiersThenTaps()
    {
        Assert.True(VboxConsoleKeyboard.TryPlanText("a", out var codes, out _));
        Assert.Equal(Clear.Concat(new[] { "1e", "9e" }), codes);
    }

    [Fact]
    public void PlanText_UpperAndShiftedSymbol()
    {
        Assert.True(VboxConsoleKeyboard.TryPlanText("Z@", out var codes, out _));
        Assert.Equal(
            Clear.Concat(new[] { "2a", "2c", "ac", "aa", "2a", "03", "83", "aa" }),
            codes);
    }

    [Fact]
    public void PlanText_EnterAndUnknown()
    {
        Assert.True(VboxConsoleKeyboard.TryPlanText("a\r\n", out var codes, out _));
        Assert.Equal(Clear.Concat(new[] { "1e", "9e", "1c", "9c" }), codes);
        Assert.False(VboxConsoleKeyboard.TryPlanText("é", out _, out var error));
        Assert.Contains("U+00E9", error, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanKey_CtrlL_AndEnter()
    {
        Assert.True(VboxConsoleKeyboard.TryPlanKey("Ctrl+L", out var chord, out _));
        Assert.Equal(Clear.Concat(new[] { "1d", "26", "a6", "9d" }), chord);
        Assert.True(VboxConsoleKeyboard.TryPlanKey("Enter", out var enter, out _));
        Assert.Equal(Clear.Concat(new[] { "1c", "9c" }), enter);
    }

    [Fact]
    public async Task TypeAsync_UsesPs2NotXdotool()
    {
        var calls = new List<string>();
        var launcher = new VirtualBoxGuestAppLauncher(
            "victoria-sandbox",
            "VBoxManage",
            (_, argv, _) =>
            {
                calls.Add(string.Join(' ', argv));
                return Task.FromResult((0, "", ""));
            },
            password: () => null,
            username: () => "victoria");

        var result = await launcher.TypeAsync("a");

        Assert.True(result.Success);
        Assert.Contains("keyboardputscancode", calls[0], StringComparison.Ordinal);
        Assert.Contains("1e", calls[0], StringComparison.Ordinal);
        Assert.DoesNotContain(calls, c => c.Contains("xdotool", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, c => c.Contains("guestcontrol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task KeyAsync_Enter_UsesPs2NotXdotool()
    {
        var calls = new List<string>();
        var launcher = new VirtualBoxGuestAppLauncher(
            "victoria-sandbox",
            "VBoxManage",
            (_, argv, _) =>
            {
                calls.Add(string.Join(' ', argv));
                return Task.FromResult((0, "", ""));
            },
            password: () => null,
            username: () => "victoria");

        var result = await launcher.KeyAsync("Enter");

        Assert.True(result.Success);
        Assert.Contains("keyboardputscancode", calls[0], StringComparison.Ordinal);
        Assert.Contains("1c", calls[0], StringComparison.Ordinal);
        Assert.DoesNotContain(calls, c => c.Contains("xdotool", StringComparison.Ordinal));
    }
}
