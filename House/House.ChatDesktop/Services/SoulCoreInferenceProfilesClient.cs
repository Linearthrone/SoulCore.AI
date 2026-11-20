using System.Net.Http;
using System.Text.Json;

namespace House.ChatDesktop.Services;

/// <summary>PROP-22.7 — one selectable model from <c>/settings/inference/profiles</c>.</summary>
public sealed class InferenceProfile
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Model { get; init; } = "";
    public bool Cloud { get; init; }

    /// <summary>Dropdown text.</summary>
    public string Display =>
        string.Equals(Label, Model, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(Label)
            ? Model + (Cloud ? " · cloud" : "")
            : $"{Label} · {Model}" + (Cloud ? " · cloud" : "");

    public override string ToString() => Display;
}

public sealed record ActiveInference(string? Model, bool Cloud, string? LeisureModel);

public sealed record InferenceProfilesFetch(
    ActiveInference? Active,
    IReadOnlyList<InferenceProfile> Profiles,
    string? Problem);

public sealed record InferenceApplyResult(bool Ok, string? Problem, ActiveInference? Active);

/// <summary>Host <c>/settings/inference/profiles</c> + <c>/apply</c> (PROP-22 §3.3). Never touches secrets.</summary>
public sealed class SoulCoreInferenceProfilesClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public async Task<InferenceProfilesFetch> GetAsync(CancellationToken ct = default)
    {
        if (!OperatorHttp.IsLoopback)
            return new InferenceProfilesFetch(null, Array.Empty<InferenceProfile>(), "Models stay on this machine.");

        try
        {
            using var request = OperatorHttp.Request(HttpMethod.Get, "/settings/inference/profiles");
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new InferenceProfilesFetch(
                    null,
                    Array.Empty<InferenceProfile>(),
                    await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false));
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Parse(json);
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new InferenceProfilesFetch(null, Array.Empty<InferenceProfile>(), $"Models unavailable: {ex.GetType().Name}");
        }
    }

    public async Task<InferenceApplyResult> ApplyAsync(string model, bool? cloud, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            return new InferenceApplyResult(false, "Pick a model first.", null);
        if (!OperatorHttp.IsLoopback)
            return new InferenceApplyResult(false, "Models stay on this machine.", null);

        try
        {
            var body = new Dictionary<string, object?> { ["model"] = model.Trim() };
            if (cloud is not null)
                body["cloud"] = cloud.Value;

            using var request = OperatorHttp.Request(HttpMethod.Post, "/settings/inference/apply", body);
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new InferenceApplyResult(false, await OperatorHttp.ErrorTextAsync(response, ct).ConfigureAwait(false), null);

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var active = doc.RootElement.TryGetProperty("active", out var a) ? ReadActive(a) : null;
            return new InferenceApplyResult(true, null, active);
        }
        catch (Exception ex) when (OperatorHttp.IsExpectedFailure(ex))
        {
            return new InferenceApplyResult(false, $"Apply failed: {ex.GetType().Name}", null);
        }
    }

    internal static InferenceProfilesFetch Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new InferenceProfilesFetch(null, Array.Empty<InferenceProfile>(), "Model list response was empty.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new InferenceProfilesFetch(null, Array.Empty<InferenceProfile>(), "Model list was not an object.");

            var active = root.TryGetProperty("active", out var a) ? ReadActive(a) : null;
            var list = new List<InferenceProfile>();
            if (root.TryGetProperty("profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in profiles.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object)
                        continue;
                    var model = OperatorHttp.Str(el, "model");
                    if (string.IsNullOrWhiteSpace(model))
                        continue;
                    list.Add(new InferenceProfile
                    {
                        Id = OperatorHttp.Str(el, "id"),
                        Label = OperatorHttp.Str(el, "label"),
                        Model = model,
                        Cloud = OperatorHttp.Bool(el, "cloud")
                    });
                }
            }

            return new InferenceProfilesFetch(active, list, null);
        }
        catch (JsonException ex)
        {
            return new InferenceProfilesFetch(null, Array.Empty<InferenceProfile>(), $"Model list JSON error: {ex.Message}");
        }
    }

    private static ActiveInference ReadActive(JsonElement el) =>
        new(OperatorHttp.StrOrNull(el, "model"), OperatorHttp.Bool(el, "cloud"), OperatorHttp.StrOrNull(el, "leisureModel"));

    public void Dispose() => _http.Dispose();
}
