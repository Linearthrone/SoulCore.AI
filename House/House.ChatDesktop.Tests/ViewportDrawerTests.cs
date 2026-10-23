using Avalonia.Controls;
using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

public class ViewportDrawerTests
{
    [Fact]
    public void Clickthrough_DefaultsOff_AndDialogsForceItOffAndPauseCua()
    {
        var policy = new ViewportInputPolicy();
        Assert.False(policy.RequestedClickthrough);
        Assert.False(policy.EffectiveClickthrough);
        Assert.False(policy.CuaPaused);

        policy.SetClickthrough(true);
        Assert.True(policy.EffectiveClickthrough);

        foreach (var kind in new[]
                 {
                     ViewportDialogKind.ToolConfirm,
                     ViewportDialogKind.QuarantineConfirm,
                     ViewportDialogKind.UpdateApply
                 })
        {
            policy.SetClickthrough(true);
            using (policy.Open(kind))
            {
                Assert.False(policy.RequestedClickthrough);
                Assert.False(policy.EffectiveClickthrough);
                Assert.True(policy.CuaPaused);
                policy.SetClickthrough(true);
                Assert.False(policy.EffectiveClickthrough);
                Assert.Contains(kind, policy.OpenDialogs);
            }

            Assert.False(policy.CuaPaused);
            Assert.False(policy.EffectiveClickthrough);
        }
    }

    [Fact]
    public void ViewOpacity_DoesNotChangeChromeOpacity()
    {
        var policy = new ViewportInputPolicy();
        var chromeBefore = policy.ChromeOpacity;
        policy.SetViewOpacity(0.3);
        Assert.Equal(0.3, policy.ViewOpacity);
        Assert.Equal(chromeBefore, policy.ChromeOpacity);
        Assert.True(policy.ViewOpacity < GlassChrome.OpacityFloor);

        policy.SetChromeOpacity(0.9);
        Assert.Equal(0.9, policy.ChromeOpacity);
        Assert.Equal(0.3, policy.ViewOpacity);

        var alpha = ViewportInputPolicy.GuestLayerAlpha(policy.ViewOpacity);
        Assert.NotNull(alpha);
        Assert.True(alpha < (byte)Math.Round(GlassChrome.OpacityFloor * 255d));
        Assert.Null(ViewportInputPolicy.GuestLayerAlpha(1));

        var settings = new LocalUiSettings
        {
            ChromeOpacity = 0.82,
            ViewOpacity = 0.3
        };
        settings.Normalize();
        Assert.Equal(0.3, settings.ViewOpacity);
        Assert.Equal(0.82, settings.ChromeOpacity);

        settings.ViewOpacity = 0;
        settings.ChromeOpacity = 0.1;
        settings.Normalize();
        Assert.Equal(ViewportInputPolicy.DefaultViewOpacity, settings.ViewOpacity);
        Assert.Equal(GlassChrome.OpacityFloor, settings.ChromeOpacity);
    }

    [Fact]
    public void Clickthrough_DetachBeforeEnable_AndRefusesParent()
    {
        var enabled = ViewportClickThrough.Describe(0, clickthrough: true);
        Assert.True(enabled.DetachGuestFirst);
        Assert.False(enabled.ParentAllowed);
        Assert.Equal(ViewportClickThrough.Scope, "viewport-guest");

        var disabled = ViewportClickThrough.Describe(enabled.ExStyleAfter, clickthrough: false);
        Assert.False(disabled.DetachGuestFirst);
        Assert.True(disabled.ParentAllowed);
        Assert.True(ViewportClickThrough.AllowsParent(0));
    }

    [Fact]
    public void Quarter_IsTopLeftHalfOfTheWorkArea()
    {
        var quarter = ViewportLayout.OfWorkArea(10, 20, 1920, 1080);
        Assert.Equal(10, quarter.X);
        Assert.Equal(20, quarter.Y);
        Assert.Equal(960, quarter.WidthPx);
        Assert.Equal(540, quarter.HeightPx);
        Assert.Equal(1920 * 1080 / 4, quarter.WidthPx * quarter.HeightPx);
    }

    [Fact]
    public void Material_RefusesFrostAndTransparency()
    {
        Assert.True(ViewportMaterial.AllowsGuest(Array.Empty<WindowTransparencyLevel>()));
        Assert.True(ViewportMaterial.AllowsGuest(new[] { WindowTransparencyLevel.None }));
        Assert.False(ViewportMaterial.AllowsGuest(new[] { WindowTransparencyLevel.AcrylicBlur }));
        Assert.False(ViewportMaterial.AllowsGuest(new[] { WindowTransparencyLevel.Transparent }));
        Assert.False(ViewportMaterial.AllowsGuest(new[]
        {
            WindowTransparencyLevel.None,
            WindowTransparencyLevel.Blur
        }));
    }

    [Fact]
    public void SetParent_StopsWhenTheFrameDoesNotMatchTheSlot()
    {
        var mismatch = GuestEmbedFit.Decide(960, 540, 1920, 1080, false, false);
        Assert.False(mismatch.Allow);
        Assert.Equal(GuestEmbedFit.ScaleStop, mismatch.Detail);

        var frosted = GuestEmbedFit.Decide(100, 100, 100, 100, false, true);
        Assert.False(frosted.Allow);
        Assert.Equal(GuestEmbedFit.FrostedStop, frosted.Detail);

        var click = GuestEmbedFit.Decide(100, 100, 100, 100, true, false);
        Assert.False(click.Allow);
        Assert.Equal(GuestEmbedFit.ClickThroughStop, click.Detail);

        var match = GuestEmbedFit.Decide(320, 240, 320, 240, false, false);
        Assert.True(match.Allow);
    }

    [Fact]
    public void SetParent_DoesNotMapClickOntoScaledFramebuffer()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var probe = GuestEmbedFit.ProbeQuarterScale();
        Assert.True(probe.Ran, probe.Detail);
        Assert.True(probe.WindowResizedToSlot, probe.Detail);
        Assert.False(probe.HostWasClickThrough, probe.Detail);
        Assert.Equal(probe.ClickX, probe.ReceivedX);
        Assert.Equal(probe.ClickY, probe.ReceivedY);
        Assert.NotEqual(probe.FrameX, probe.ReceivedX);
        Assert.NotEqual(probe.FrameY, probe.ReceivedY);
        Assert.False(probe.ScaledClickHonest, probe.Detail);
    }

    [Fact]
    public void Still_IsNotParented_AndStaysOpaque()
    {
        Assert.False(ViewportStill.HostsGuestHwnd);
        Assert.False(ViewportStill.AllowsClickthroughOnStill);
        Assert.False(ViewportStill.Frosted);
        Assert.Equal(1d, ViewportStill.WindowOpacity);
        Assert.Equal(1d, ViewportStill.BorderDip);
        Assert.Equal(255, ViewportStill.FillColor.A);
        Assert.Contains("still", ViewportStill.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a live guest", ViewportStill.Label, StringComparison.OrdinalIgnoreCase);

        var live = ViewportStill.LiveView();
        Assert.True(live.FocusStillInChatDrawer);
        Assert.False(live.ParentHwnd);
        Assert.True(live.HerScreenStaysReachable);

        Assert.False(ViewportStill.MayParent(960, 540, 1920, 1080));
        Assert.False(ViewportStill.MayParent(320, 240, 320, 240));

        var mismatch = ViewportStill.Present(960, 540, 1920, 1080, clickthroughRequested: true);
        Assert.False(mismatch.ParentHwnd);
        Assert.True(mismatch.Opaque);
        Assert.False(mismatch.Frosted);
        Assert.Equal(1d, mismatch.WindowOpacity);
        Assert.False(mismatch.ClickthroughApplied);
        Assert.True(mismatch.ClickthroughRequested);
        Assert.Equal(GuestEmbedFit.ScaleStop, mismatch.StopLine);
        Assert.Equal(ViewportStill.Label, mismatch.Label);

        var match = ViewportStill.Present(320, 240, 320, 240, clickthroughRequested: false);
        Assert.False(match.ParentHwnd);
        Assert.False(match.ClickthroughApplied);
        Assert.Null(match.StopLine);

        var policy = new ViewportInputPolicy();
        Assert.False(policy.RequestedClickthrough);
        Assert.False(policy.EffectiveClickthrough);
    }
}
