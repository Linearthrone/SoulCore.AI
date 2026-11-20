namespace House.ChatDesktop.Services;

/// <summary>The eight services the status orb / radial popup report on.</summary>
public enum StatusService
{
    Host,
    Companion,
    Inference,
    Unreal,
    ComfyUi,
    Vm,
    Voice,
    Mailbox
}

/// <summary>
/// Light for one service. <see cref="Off"/> means intentionally off / not configured —
/// it is dim and never counts as a down.
/// </summary>
public enum ServiceLight
{
    Off,
    Up,
    Down
}

/// <summary>Orb state, in priority order (PROP-22 §3.6).</summary>
public enum OrbState
{
    /// <summary>Everything counted is up.</summary>
    Green,
    /// <summary>Exactly one non-critical service is down.</summary>
    Yellow,
    /// <summary>Two or more non-critical services are down.</summary>
    Amber,
    /// <summary>Slow pink pulse: she is busy (activity / chat turn / call). Never hides red.</summary>
    PinkPulse,
    /// <summary>Solid red: companion WS or the active inference path is down.</summary>
    Red,
    /// <summary>Fast red flash: Host down / <c>/health</c> unreachable.</summary>
    FastRedFlash
}

public sealed record ServiceStatus(
    StatusService Service,
    string Label,
    ServiceLight Light,
    bool Critical,
    string Detail);

/// <summary>Raw facts the aggregator turns into eight lights. All of them come from real probes.</summary>
public sealed class StatusFacts
{
    public bool HostUp { get; init; }
    public string? HostDetail { get; init; }

    public bool WsConnected { get; init; }
    public string? WsDetail { get; init; }

    public bool InferenceEnabled { get; init; }
    public bool InferenceCloud { get; init; }
    public bool OllamaUp { get; init; }
    public string? InferenceModel { get; init; }

    public bool? UnrealEnabled { get; init; }
    public bool? UnrealConnected { get; init; }

    public bool ComfyUp { get; init; }
    public bool VmRunning { get; init; }

    public bool VoiceEnabled { get; init; }
    public bool VoiceSttOk { get; init; }
    public bool VoiceTtsOk { get; init; }

    public int MailboxesEnabled { get; init; }
    public int MailboxesConfigured { get; init; }

    /// <summary><c>presence.currentActivity</c> from Host, or null when she has nothing open.</summary>
    public string? CurrentActivity { get; init; }
    public bool CallActive { get; init; }
    public bool TypingActive { get; init; }
}

/// <summary>
/// PROP-22.5 — computes the status orb from real probe results. Pure; no UI types.
/// Priority: Host down (fast red) → WS/inference (solid red) → busy (pink) →
/// ≥2 non-critical down (amber) → 1 non-critical down (yellow) → green.
/// </summary>
public static class SystemStatusAggregator
{
    public static IReadOnlyList<ServiceStatus> BuildServices(StatusFacts f)
    {
        ArgumentNullException.ThrowIfNull(f);

        // Without Host nothing downstream is knowable — report Host down, the rest off-line
        // dim so a dead Host does not also paint seven fake downs.
        var hostLight = f.HostUp ? ServiceLight.Up : ServiceLight.Down;

        var companion = !f.HostUp
            ? ServiceLight.Off
            : f.WsConnected ? ServiceLight.Up : ServiceLight.Down;

        // Inference: disabled in Host = intentionally off. Cloud model: Host reachable is the
        // best signal we have. Local model: Ollama must answer.
        ServiceLight inference;
        string inferenceDetail;
        if (!f.HostUp)
        {
            inference = ServiceLight.Off;
            inferenceDetail = "Host down";
        }
        else if (!f.InferenceEnabled)
        {
            inference = ServiceLight.Off;
            inferenceDetail = "off in Host";
        }
        else if (f.InferenceCloud)
        {
            inference = ServiceLight.Up;
            inferenceDetail = Describe(f.InferenceModel, "cloud");
        }
        else
        {
            inference = f.OllamaUp ? ServiceLight.Up : ServiceLight.Down;
            inferenceDetail = f.OllamaUp
                ? Describe(f.InferenceModel, "local")
                : "Ollama not answering";
        }

        var unreal = !f.HostUp || f.UnrealEnabled != true
            ? ServiceLight.Off
            : f.UnrealConnected == true ? ServiceLight.Up : ServiceLight.Down;

        // No Host flag says "ComfyUI is expected", so a silent :8188 is treated as off, not down.
        var comfy = f.ComfyUp ? ServiceLight.Up : ServiceLight.Off;

        // Presence never auto-starts the VM — a stopped VM is a choice, not a failure.
        var vm = f.VmRunning ? ServiceLight.Up : ServiceLight.Off;

        ServiceLight voice;
        string voiceDetail;
        if (!f.HostUp || !f.VoiceEnabled)
        {
            voice = ServiceLight.Off;
            voiceDetail = f.HostUp ? "off in Host" : "Host down";
        }
        else if (f.VoiceSttOk && f.VoiceTtsOk)
        {
            voice = ServiceLight.Up;
            voiceDetail = "STT ok · TTS ok";
        }
        else
        {
            voice = ServiceLight.Down;
            voiceDetail = $"STT {(f.VoiceSttOk ? "ok" : "down")} · TTS {(f.VoiceTtsOk ? "ok" : "down")}";
        }

        ServiceLight mailbox;
        string mailboxDetail;
        if (f.MailboxesEnabled == 0)
        {
            mailbox = ServiceLight.Off;
            mailboxDetail = "no mailbox";
        }
        else if (f.MailboxesConfigured >= f.MailboxesEnabled)
        {
            mailbox = ServiceLight.Up;
            mailboxDetail = $"{f.MailboxesConfigured} ready";
        }
        else
        {
            mailbox = ServiceLight.Down;
            mailboxDetail = $"{f.MailboxesConfigured}/{f.MailboxesEnabled} ready";
        }

        return new[]
        {
            new ServiceStatus(StatusService.Host, "Host", hostLight, true,
                f.HostUp ? "answering /health" : (f.HostDetail ?? "not answering")),
            new ServiceStatus(StatusService.Companion, "Companion", companion, true,
                !f.HostUp ? "Host down" : f.WsConnected ? "WS connected" : (f.WsDetail ?? "WS down")),
            new ServiceStatus(StatusService.Inference, "Inference", inference, true, inferenceDetail),
            new ServiceStatus(StatusService.Unreal, "Unreal", unreal, false,
                unreal switch
                {
                    ServiceLight.Up => "connected",
                    ServiceLight.Down => "enabled, not connected",
                    _ => "off"
                }),
            new ServiceStatus(StatusService.ComfyUi, "ComfyUI", comfy, false,
                f.ComfyUp ? ":8188 answering" : "off"),
            new ServiceStatus(StatusService.Vm, "VM", vm, false,
                f.VmRunning ? "KsDevice running" : "stopped"),
            new ServiceStatus(StatusService.Voice, "Voice", voice, false, voiceDetail),
            new ServiceStatus(StatusService.Mailbox, "Mailbox", mailbox, false, mailboxDetail)
        };
    }

    /// <summary>True when she is doing something: Host activity, a live call, or a chat turn.</summary>
    public static bool IsBusy(StatusFacts f) =>
        !string.IsNullOrWhiteSpace(f.CurrentActivity) || f.CallActive || f.TypingActive;

    public static OrbState ComputeOrb(IReadOnlyList<ServiceStatus> services, bool busy)
    {
        ArgumentNullException.ThrowIfNull(services);

        var host = services.FirstOrDefault(s => s.Service == StatusService.Host);
        if (host is null || host.Light == ServiceLight.Down)
            return OrbState.FastRedFlash;

        var criticalDown = services.Any(s =>
            s.Light == ServiceLight.Down
            && s.Service is StatusService.Companion or StatusService.Inference);
        if (criticalDown)
            return OrbState.Red;

        if (busy)
            return OrbState.PinkPulse;

        var nonCriticalDowns = services.Count(s => !s.Critical && s.Light == ServiceLight.Down);
        return nonCriticalDowns switch
        {
            0 => OrbState.Green,
            1 => OrbState.Yellow,
            _ => OrbState.Amber
        };
    }

    public static OrbState Compute(StatusFacts f) =>
        ComputeOrb(BuildServices(f), IsBusy(f));

    public static string Describe(OrbState state) =>
        state switch
        {
            OrbState.FastRedFlash => "Host down",
            OrbState.Red => "Companion or inference down",
            OrbState.PinkPulse => "Busy",
            OrbState.Amber => "Two or more services down",
            OrbState.Yellow => "One service down",
            OrbState.Green => "All good",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

    private static string Describe(string? model, string kind) =>
        string.IsNullOrWhiteSpace(model) ? kind : $"{model.Trim()} · {kind}";
}
