using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Companion;

public sealed record CompanionMediaAsset(
    string MediaId,
    string ContactId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    string? Prompt);

public interface ICompanionMediaService
{
    Task<IReadOnlyList<object>> ListModelsAsync(CancellationToken ct = default);
    Task<CompanionMediaAsset> GenerateAsync(
        string positivePrompt,
        string? negativePrompt,
        string? model,
        string? contactId,
        CancellationToken ct = default);
    bool TryGetFile(string mediaId, out string fullPath, out CompanionMediaAsset? meta);
    Task PushGeneratedToChatAsync(
        string mediaId,
        string? caption,
        CancellationToken ct = default);
}

/// <summary>Stores ComfyUI outputs under LocalAppData and optional chat push.</summary>
public sealed class CompanionMediaService : ICompanionMediaService
{
    private readonly ComfyUiClient _comfy;
    private readonly CompanionOptions _options;
    private readonly ICompanionOutboundMessenger _outbound;
    private readonly ILogger<CompanionMediaService> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, CompanionMediaAsset> _index = new(StringComparer.OrdinalIgnoreCase);

    public CompanionMediaService(
        ComfyUiClient comfy,
        IOptions<CompanionOptions> options,
        ICompanionOutboundMessenger outbound,
        ILogger<CompanionMediaService> logger)
    {
        _comfy = comfy ?? throw new ArgumentNullException(nameof(comfy));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _outbound = outbound ?? throw new ArgumentNullException(nameof(outbound));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Directory.CreateDirectory(_options.ResolveMediaStorePath());
        LoadIndex();
    }

    public async Task<IReadOnlyList<object>> ListModelsAsync(CancellationToken ct = default)
    {
        var healthy = await _comfy.IsHealthyAsync(ct).ConfigureAwait(false);
        if (!healthy)
        {
            return new object[]
            {
                new
                {
                    id = "comfyui",
                    label = "ComfyUI (offline)",
                    available = false,
                    baseUrl = _options.ComfyUiBaseUrl
                }
            };
        }

        var ckpts = await _comfy.ListCheckpointsAsync(ct).ConfigureAwait(false);
        if (ckpts.Count == 0)
        {
            return new object[]
            {
                new
                {
                    id = "comfyui",
                    label = "ComfyUI (no checkpoints)",
                    available = true,
                    baseUrl = _options.ComfyUiBaseUrl
                }
            };
        }

        return ckpts.Select(c => (object)new
        {
            id = c,
            label = c,
            available = true,
            provider = "comfyui"
        }).ToList();
    }

    public async Task<CompanionMediaAsset> GenerateAsync(
        string positivePrompt,
        string? negativePrompt,
        string? model,
        string? contactId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(positivePrompt))
            throw new ArgumentException("positivePrompt required", nameof(positivePrompt));

        var contact = string.IsNullOrWhiteSpace(contactId)
            ? _options.DefaultContactId
            : contactId.Trim();

        var png = await _comfy
            .GeneratePngAsync(positivePrompt.Trim(), negativePrompt, model, ct: ct)
            .ConfigureAwait(false);

        var mediaId = Guid.NewGuid().ToString("N");
        var fileName = mediaId + ".png";
        var dir = _options.ResolveMediaStorePath();
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(fullPath, png, ct).ConfigureAwait(false);

        var asset = new CompanionMediaAsset(
            mediaId,
            contact,
            fileName,
            "image/png",
            png.LongLength,
            DateTimeOffset.UtcNow,
            positivePrompt.Trim());

        lock (_gate)
            _index[mediaId] = asset;
        PersistIndex();

        _logger.LogInformation("Companion media stored id={MediaId} bytes={Bytes}", mediaId, png.Length);
        return asset;
    }

    public bool TryGetFile(string mediaId, out string fullPath, out CompanionMediaAsset? meta)
    {
        fullPath = "";
        meta = null;
        if (string.IsNullOrWhiteSpace(mediaId))
            return false;

        lock (_gate)
        {
            if (!_index.TryGetValue(mediaId.Trim(), out meta))
                return false;
        }

        fullPath = Path.Combine(_options.ResolveMediaStorePath(), meta!.FileName);
        return File.Exists(fullPath);
    }

    public async Task PushGeneratedToChatAsync(
        string mediaId,
        string? caption,
        CancellationToken ct = default)
    {
        if (!TryGetFile(mediaId, out _, out var meta) || meta is null)
            throw new FileNotFoundException("Unknown mediaId", mediaId);

        var text = string.IsNullOrWhiteSpace(caption)
            ? "I made something for you."
            : caption.Trim();

        await _outbound
            .PushAsync(text, meta.ContactId, mediaId, streamDelta: false, ct)
            .ConfigureAwait(false);
    }

    private void LoadIndex()
    {
        var path = IndexPath();
        if (!File.Exists(path))
            return;
        try
        {
            var json = File.ReadAllText(path);
            var list = System.Text.Json.JsonSerializer.Deserialize<List<CompanionMediaAsset>>(json);
            if (list is null) return;
            lock (_gate)
            {
                foreach (var a in list)
                    _index[a.MediaId] = a;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Companion media index load failed");
        }
    }

    private void PersistIndex()
    {
        try
        {
            List<CompanionMediaAsset> snapshot;
            lock (_gate)
                snapshot = _index.Values.OrderByDescending(a => a.CreatedAtUtc).Take(500).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
            File.WriteAllText(IndexPath(), json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Companion media index persist failed");
        }
    }

    private string IndexPath() =>
        Path.Combine(_options.ResolveMediaStorePath(), "index.json");
}
