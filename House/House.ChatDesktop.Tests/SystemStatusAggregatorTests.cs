using House.ChatDesktop.Services;

namespace House.ChatDesktop.Tests;

/// <summary>PROP-22.5 — status orb priority and the "intentionally off is never a problem" rule.</summary>
public sealed class SystemStatusAggregatorTests
{
    private static StatusFacts Healthy(Func<StatusFacts, StatusFacts>? tweak = null)
    {
        var f = new StatusFacts
        {
            HostUp = true,
            WsConnected = true,
            InferenceEnabled = true,
            InferenceCloud = true,
            InferenceModel = "test-model"
        };
        return tweak is null ? f : tweak(f);
    }

    private static StatusFacts With(
        bool hostUp = true,
        bool ws = true,
        bool inference = true,
        bool cloud = true,
        bool ollama = false,
        bool? unrealEnabled = null,
        bool? unrealConnected = null,
        bool comfy = false,
        bool vm = false,
        bool voiceEnabled = false,
        bool stt = false,
        bool tts = false,
        int mailEnabled = 0,
        int mailConfigured = 0,
        string? activity = null,
        bool call = false,
        bool typing = false) =>
        new()
        {
            HostUp = hostUp,
            WsConnected = ws,
            InferenceEnabled = inference,
            InferenceCloud = cloud,
            OllamaUp = ollama,
            UnrealEnabled = unrealEnabled,
            UnrealConnected = unrealConnected,
            ComfyUp = comfy,
            VmRunning = vm,
            VoiceEnabled = voiceEnabled,
            VoiceSttOk = stt,
            VoiceTtsOk = tts,
            MailboxesEnabled = mailEnabled,
            MailboxesConfigured = mailConfigured,
            CurrentActivity = activity,
            CallActive = call,
            TypingActive = typing
        };

    [Fact]
    public void Healthy_stack_is_green()
    {
        Assert.Equal(OrbState.Green, SystemStatusAggregator.Compute(Healthy()));
    }

    [Fact]
    public void Intentionally_off_services_never_color_the_orb()
    {
        // VM stopped, ComfyUI silent, Unreal disabled, voice disabled, no mailbox: all "off", all fine.
        var f = With(vm: false, comfy: false, unrealEnabled: false, voiceEnabled: false, mailEnabled: 0);
        Assert.Equal(OrbState.Green, SystemStatusAggregator.Compute(f));

        var lights = SystemStatusAggregator.BuildServices(f)
            .Where(s => !s.Critical)
            .Select(s => s.Light);
        Assert.DoesNotContain(ServiceLight.Down, lights);
    }

    [Fact]
    public void Host_down_is_fast_red_flash_even_when_busy()
    {
        var f = With(hostUp: false, ws: false, activity: "writing a note", call: true);
        Assert.Equal(OrbState.FastRedFlash, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Host_down_does_not_paint_the_rest_as_downs()
    {
        var services = SystemStatusAggregator.BuildServices(With(hostUp: false, ws: false));
        Assert.Equal(1, services.Count(s => s.Light == ServiceLight.Down));
        Assert.Equal(ServiceLight.Down, services.First(s => s.Service == StatusService.Host).Light);
    }

    [Fact]
    public void Websocket_down_is_solid_red_and_beats_pink()
    {
        var f = With(ws: false, activity: "researching");
        Assert.Equal(OrbState.Red, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Local_inference_down_is_solid_red()
    {
        var f = With(cloud: false, ollama: false);
        Assert.Equal(OrbState.Red, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Local_inference_up_is_not_red()
    {
        var f = With(cloud: false, ollama: true);
        Assert.Equal(OrbState.Green, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Inference_disabled_in_host_is_off_not_red()
    {
        var f = With(inference: false);
        var inference = SystemStatusAggregator.BuildServices(f)
            .First(s => s.Service == StatusService.Inference);
        Assert.Equal(ServiceLight.Off, inference.Light);
        Assert.Equal(OrbState.Green, SystemStatusAggregator.Compute(f));
    }

    [Theory]
    [InlineData("reading mail", false, false)]
    [InlineData(null, true, false)]
    [InlineData(null, false, true)]
    public void Activity_call_or_typing_pulses_pink(string? activity, bool call, bool typing)
    {
        var f = With(activity: activity, call: call, typing: typing);
        Assert.Equal(OrbState.PinkPulse, SystemStatusAggregator.Compute(f));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_activity_is_not_busy(string? activity)
    {
        Assert.False(SystemStatusAggregator.IsBusy(With(activity: activity)));
        Assert.Equal(OrbState.Green, SystemStatusAggregator.Compute(With(activity: activity)));
    }

    [Fact]
    public void One_noncritical_down_is_yellow()
    {
        // Unreal enabled but not connected.
        var f = With(unrealEnabled: true, unrealConnected: false);
        Assert.Equal(OrbState.Yellow, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Two_noncritical_down_is_amber()
    {
        var f = With(unrealEnabled: true, unrealConnected: false, mailEnabled: 2, mailConfigured: 1);
        Assert.Equal(OrbState.Amber, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Voice_enabled_with_stt_down_counts_as_one_noncritical_down()
    {
        var f = With(voiceEnabled: true, stt: false, tts: true);
        Assert.Equal(OrbState.Yellow, SystemStatusAggregator.Compute(f));
    }

    [Fact]
    public void Busy_hides_yellow_and_amber_but_never_red()
    {
        var busyYellow = With(unrealEnabled: true, unrealConnected: false, activity: "working");
        Assert.Equal(OrbState.PinkPulse, SystemStatusAggregator.Compute(busyYellow));

        var busyRed = With(ws: false, unrealEnabled: true, unrealConnected: false, activity: "working");
        Assert.Equal(OrbState.Red, SystemStatusAggregator.Compute(busyRed));
    }

    [Fact]
    public void Every_orb_state_has_a_description()
    {
        foreach (var state in Enum.GetValues<OrbState>())
            Assert.False(string.IsNullOrWhiteSpace(SystemStatusAggregator.Describe(state)));
    }

    [Fact]
    public void Eight_services_are_reported_in_a_stable_order()
    {
        var services = SystemStatusAggregator.BuildServices(Healthy());
        Assert.Equal(
            new[]
            {
                StatusService.Host, StatusService.Companion, StatusService.Inference, StatusService.Unreal,
                StatusService.ComfyUi, StatusService.Vm, StatusService.Voice, StatusService.Mailbox
            },
            services.Select(s => s.Service).ToArray());
    }
}
