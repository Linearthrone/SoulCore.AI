using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Companion;

/// <summary>
/// Minimal ComfyUI HTTP client (prompt → poll history → download /view).
/// Ported from LLMOD <c>GenerateImageViaComfyUiNativeAsync</c> patterns.
/// </summary>
public sealed class ComfyUiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly CompanionOptions _options;
    private readonly ILogger<ComfyUiClient> _logger;

    public ComfyUiClient(
        HttpClient http,
        IOptions<CompanionOptions> options,
        ILogger<ComfyUiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<string>> ListCheckpointsAsync(CancellationToken ct = default)
    {
        var baseUrl = _options.ComfyUiBaseUrl.TrimEnd('/');
        using var resp = await _http.GetAsync($"{baseUrl}/object_info/CheckpointLoaderSimple", ct)
            .ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return Array.Empty<string>();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("CheckpointLoaderSimple", out var loader)
            || !loader.TryGetProperty("input", out var input)
            || !input.TryGetProperty("required", out var required)
            || !required.TryGetProperty("ckpt_name", out var ckpt))
        {
            return Array.Empty<string>();
        }

        // ckpt_name is typically [["model1.safetensors", "model2…"], {…}]
        if (ckpt.ValueKind == JsonValueKind.Array && ckpt.GetArrayLength() > 0)
        {
            var first = ckpt[0];
            if (first.ValueKind == JsonValueKind.Array)
            {
                return first.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
        }

        return Array.Empty<string>();
    }

    public async Task<byte[]> GeneratePngAsync(
        string positivePrompt,
        string? negativePrompt = null,
        string? checkpoint = null,
        int? width = null,
        int? height = null,
        CancellationToken ct = default)
    {
        var baseUrl = _options.ComfyUiBaseUrl.TrimEnd('/');
        var w = width is > 0 ? width.Value : _options.DefaultWidth;
        var h = height is > 0 ? height.Value : _options.DefaultHeight;
        var seed = Random.Shared.Next(1, int.MaxValue);
        var prefix = "soulcore_" + Guid.NewGuid().ToString("N")[..8];

        var ckpt = checkpoint;
        if (string.IsNullOrWhiteSpace(ckpt))
            ckpt = _options.ComfyUiPreferredCheckpoint;
        if (string.IsNullOrWhiteSpace(ckpt))
        {
            var list = await ListCheckpointsAsync(ct).ConfigureAwait(false);
            ckpt = list.FirstOrDefault()
                ?? throw new InvalidOperationException("ComfyUI has no checkpoints loaded.");
        }

        Dictionary<string, object> workflow;
        if (!string.IsNullOrWhiteSpace(_options.ComfyUiWorkflowPath)
            && File.Exists(_options.ComfyUiWorkflowPath))
        {
            var raw = await File.ReadAllTextAsync(_options.ComfyUiWorkflowPath, ct).ConfigureAwait(false);
            raw = raw
                .Replace("{{positive}}", positivePrompt, StringComparison.Ordinal)
                .Replace("{{negative}}", negativePrompt ?? "low quality, blurry, watermark, text, ugly", StringComparison.Ordinal)
                .Replace("{{seed}}", seed.ToString(), StringComparison.Ordinal)
                .Replace("{{width}}", w.ToString(), StringComparison.Ordinal)
                .Replace("{{height}}", h.ToString(), StringComparison.Ordinal)
                .Replace("{{filename_prefix}}", prefix, StringComparison.Ordinal);
            workflow = LoadWorkflow(raw);
        }
        else
        {
            workflow = BuildTxt2ImgWorkflow(
                ckpt!,
                positivePrompt,
                negativePrompt ?? "low quality, blurry, watermark, text, ugly",
                w,
                h,
                seed,
                prefix);
        }

        var payload = new { prompt = workflow };
        using var post = await _http.PostAsJsonAsync($"{baseUrl}/prompt", payload, JsonOpts, ct)
            .ConfigureAwait(false);
        var postBody = await post.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!post.IsSuccessStatusCode)
            throw new InvalidOperationException($"ComfyUI /prompt failed HTTP {(int)post.StatusCode}: {Truncate(postBody, 200)}");

        using var postDoc = JsonDocument.Parse(postBody);
        if (!postDoc.RootElement.TryGetProperty("prompt_id", out var idEl))
            throw new InvalidOperationException("ComfyUI /prompt response missing prompt_id.");
        var promptId = idEl.GetString()
            ?? throw new InvalidOperationException("ComfyUI prompt_id empty.");

        var (filename, subfolder, type) = await PollForOutputAsync(baseUrl, promptId, ct)
            .ConfigureAwait(false);

        var viewUrl =
            $"{baseUrl}/view?filename={Uri.EscapeDataString(filename)}" +
            $"&subfolder={Uri.EscapeDataString(subfolder ?? "")}" +
            $"&type={Uri.EscapeDataString(type ?? "output")}";
        using var view = await _http.GetAsync(viewUrl, ct).ConfigureAwait(false);
        view.EnsureSuccessStatusCode();
        var bytes = await view.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        _logger.LogInformation(
            "ComfyUI generated {Bytes} bytes prompt={PromptId} file={File}",
            bytes.Length,
            promptId,
            filename);
        return bytes;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var baseUrl = _options.ComfyUiBaseUrl.TrimEnd('/');
            using var resp = await _http.GetAsync($"{baseUrl}/system_stats", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(string filename, string subfolder, string type)> PollForOutputAsync(
        string baseUrl,
        string promptId,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(_options.ComfyUiTimeoutSeconds, 30, 600));
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            using var hist = await _http.GetAsync($"{baseUrl}/history/{promptId}", ct).ConfigureAwait(false);
            if (hist.IsSuccessStatusCode)
            {
                await using var stream = await hist.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty(promptId, out var entry)
                    && TryExtractFirstImage(entry, out var img))
                {
                    return img;
                }
            }

            await Task.Delay(1500, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"ComfyUI prompt {promptId} timed out waiting for output.");
    }

    private static bool TryExtractFirstImage(
        JsonElement entry,
        out (string filename, string subfolder, string type) image)
    {
        image = default;
        if (!entry.TryGetProperty("outputs", out var outputs))
            return false;

        foreach (var node in outputs.EnumerateObject())
        {
            if (!node.Value.TryGetProperty("images", out var images)
                || images.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var img in images.EnumerateArray())
            {
                var filename = img.TryGetProperty("filename", out var f) ? f.GetString() : null;
                if (string.IsNullOrWhiteSpace(filename))
                    continue;
                var subfolder = img.TryGetProperty("subfolder", out var s) ? s.GetString() ?? "" : "";
                var type = img.TryGetProperty("type", out var t) ? t.GetString() ?? "output" : "output";
                image = (filename!, subfolder, type);
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, object> LoadWorkflow(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("prompt", out var promptEl))
            root = promptEl;
        return JsonElementToDictionary(root)
            ?? throw new InvalidOperationException("Workflow JSON empty.");
    }

    private static Dictionary<string, object>? JsonElementToDictionary(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        var dict = new Dictionary<string, object>();
        foreach (var prop in el.EnumerateObject())
            dict[prop.Name] = JsonElementToObject(prop.Value)!;
        return dict;
    }

    private static object? JsonElementToObject(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDictionary(el),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

    private static Dictionary<string, object> BuildTxt2ImgWorkflow(
        string ckptName,
        string positive,
        string negative,
        int width,
        int height,
        int seed,
        string filenamePrefix)
    {
        object Link(string nodeId, int slot) => new object[] { nodeId, slot };

        return new Dictionary<string, object>
        {
            ["4"] = new Dictionary<string, object>
            {
                ["class_type"] = "CheckpointLoaderSimple",
                ["inputs"] = new Dictionary<string, object> { ["ckpt_name"] = ckptName }
            },
            ["5"] = new Dictionary<string, object>
            {
                ["class_type"] = "EmptyLatentImage",
                ["inputs"] = new Dictionary<string, object>
                {
                    ["width"] = width,
                    ["height"] = height,
                    ["batch_size"] = 1
                }
            },
            ["6"] = new Dictionary<string, object>
            {
                ["class_type"] = "CLIPTextEncode",
                ["inputs"] = new Dictionary<string, object>
                {
                    ["text"] = positive,
                    ["clip"] = Link("4", 1)
                }
            },
            ["7"] = new Dictionary<string, object>
            {
                ["class_type"] = "CLIPTextEncode",
                ["inputs"] = new Dictionary<string, object>
                {
                    ["text"] = negative,
                    ["clip"] = Link("4", 1)
                }
            },
            ["3"] = new Dictionary<string, object>
            {
                ["class_type"] = "KSampler",
                ["inputs"] = new Dictionary<string, object>
                {
                    ["seed"] = seed,
                    ["steps"] = 20,
                    ["cfg"] = 7,
                    ["sampler_name"] = "euler",
                    ["scheduler"] = "simple",
                    ["denoise"] = 1.0,
                    ["model"] = Link("4", 0),
                    ["positive"] = Link("6", 0),
                    ["negative"] = Link("7", 0),
                    ["latent_image"] = Link("5", 0)
                }
            },
            ["8"] = new Dictionary<string, object>
            {
                ["class_type"] = "VAEDecode",
                ["inputs"] = new Dictionary<string, object>
                {
                    ["samples"] = Link("3", 0),
                    ["vae"] = Link("4", 2)
                }
            },
            ["9"] = new Dictionary<string, object>
            {
                ["class_type"] = "SaveImage",
                ["inputs"] = new Dictionary<string, object>
                {
                    ["filename_prefix"] = filenamePrefix,
                    ["images"] = Link("8", 0)
                }
            }
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
