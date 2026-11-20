using SoulCore.Config;

namespace SoulCore.Host.Inference;

/// <summary>
/// PROP-22.2: thread-safe, in-memory live override of the chat model / cloud flag.
/// <c>null</c> means "use <see cref="InferenceOptions"/> defaults". Never persisted and never
/// touches secrets. Leisure model always comes from <see cref="InferenceOptions.LeisureModel"/>.
/// </summary>
public sealed class InferenceRuntimeSelection
{
    private readonly object _gate = new();
    private string? _model;
    private bool? _cloud;

    /// <summary>Current override model, or <c>null</c> when defaults apply.</summary>
    public string? ModelOverride
    {
        get { lock (_gate) return _model; }
    }

    /// <summary>Current override cloud flag, or <c>null</c> when defaults apply.</summary>
    public bool? CloudOverride
    {
        get { lock (_gate) return _cloud; }
    }

    /// <summary>Resolve the effective chat selection against configured <paramref name="opts"/>.</summary>
    public (string Model, bool Cloud, string LeisureModel, string BaseUrlHint) GetEffective(InferenceOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        string? model;
        bool? cloud;
        lock (_gate)
        {
            model = _model;
            cloud = _cloud;
        }

        var effectiveModel = string.IsNullOrWhiteSpace(model) ? opts.Model : model;
        var effectiveCloud = cloud ?? opts.IsCloudEndpoint;
        return (
            effectiveModel,
            effectiveCloud,
            opts.LeisureModel,
            effectiveCloud ? InferenceOptions.CloudBaseUrl : "loopback");
    }

    /// <summary>Set the live override. Blank <paramref name="model"/> clears the model override; <paramref name="cloud"/> null leaves cloud on defaults.</summary>
    public void Apply(string? model, bool? cloud = null)
    {
        lock (_gate)
        {
            _model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
            _cloud = cloud;
        }
    }

    /// <summary>Drop any override; <see cref="InferenceOptions"/> defaults apply again.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _model = null;
            _cloud = null;
        }
    }
}
