using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Inference.Tools.Browser;
using SoulCore.Inference.Tools.Desktop;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.3: in-memory outbound SMS/MMS queue + optional webhook. Tablet polls pending jobs.
/// This is the mockable gateway seam for CI (no Android required).
/// </summary>
public sealed class SmsOutboundService : ISmsOutboundService
{
    private readonly SmsOptions _sms;
    private readonly IVictoriaBrowserViewHub? _browserView;
    private readonly IDesktopViewHub? _desktopView;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<SmsOutboundService> _logger;
    private readonly object _gate = new();
    private readonly List<SmsOutboundJob> _jobs = new();
    private readonly List<DateTimeOffset> _smsMarks = new();
    private readonly List<DateTimeOffset> _mmsMarks = new();
    private DateTimeOffset _lastSmsUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastMmsUtc = DateTimeOffset.MinValue;

    public SmsOutboundService(
        IOptions<SmsOptions> sms,
        ILogger<SmsOutboundService> logger,
        IVictoriaBrowserViewHub? browserView = null,
        IDesktopViewHub? desktopView = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        _sms = sms?.Value ?? throw new ArgumentNullException(nameof(sms));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _browserView = browserView;
        _desktopView = desktopView;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SmsOutboundEnqueueResult> EnqueueSmsAsync(
        string toE164,
        string text,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sms.OutboundEnabled)
            return new SmsOutboundEnqueueResult(false, null, false, "outbound_disabled");

        var to = SmsE164.Normalize(toE164);
        if (!SmsE164.IsAllowlisted(to, _sms.KurtAllowlistE164))
            return new SmsOutboundEnqueueResult(false, null, false, "not_allowlisted");

        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
            return new SmsOutboundEnqueueResult(false, null, false, "text_required");
        body = SmsInboundService.TruncateForSms(body, 480);

        if (!TryTakeSmsSlot(out var rateErr))
        {
            RecordDropped(SmsOutboundKind.Sms, to, body, null, null, source, rateErr);
            return new SmsOutboundEnqueueResult(false, null, true, rateErr);
        }

        var job = NewJob(SmsOutboundKind.Sms, to, body, null, null, source);
        StorePending(job);
        await TryWebhookAsync(job, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "SMS outbound enqueued id={Id} to={To} chars={Chars} source={Source}",
            job.Id,
            SmsE164.Redact(to),
            body.Length,
            source ?? "(none)");

        return new SmsOutboundEnqueueResult(true, job.Id, false, null);
    }

    public async Task<SmsOutboundEnqueueResult> EnqueueMmsAsync(
        string toE164,
        byte[] imageBytes,
        string contentType,
        string? caption = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sms.OutboundEnabled)
            return new SmsOutboundEnqueueResult(false, null, false, "outbound_disabled");

        var to = SmsE164.Normalize(toE164);
        if (!SmsE164.IsAllowlisted(to, _sms.KurtAllowlistE164))
            return new SmsOutboundEnqueueResult(false, null, false, "not_allowlisted");

        if (imageBytes is null || imageBytes.Length == 0)
            return new SmsOutboundEnqueueResult(false, null, false, "image_required");

        (imageBytes, contentType) = SmsMmsImageSanitizer.SanitizeForOutbound(imageBytes, contentType);
        var ct = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType.Trim();
        var cap = string.IsNullOrWhiteSpace(caption)
            ? null
            : SmsInboundService.TruncateForSms(caption.Trim(), 200);

        if (!TryTakeMmsSlot(out var rateErr))
        {
            RecordDropped(SmsOutboundKind.Mms, to, cap, imageBytes, ct, source, rateErr);
            return new SmsOutboundEnqueueResult(false, null, true, rateErr);
        }

        var job = NewJob(SmsOutboundKind.Mms, to, cap, imageBytes, ct, source);
        StorePending(job);
        await TryWebhookAsync(job, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "MMS outbound enqueued id={Id} to={To} bytes={Bytes} source={Source}",
            job.Id,
            SmsE164.Redact(to),
            imageBytes.Length,
            source ?? "(none)");

        return new SmsOutboundEnqueueResult(true, job.Id, false, null);
    }

    public async Task<SmsOutboundEnqueueResult> EnqueueScreenshotMmsToKurtAsync(
        string? caption = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var kurt = FirstAllowlistedKurt();
        if (kurt is null)
            return new SmsOutboundEnqueueResult(false, null, false, "allowlist_empty");

        if (!TryCaptureStill(out var bytes, out var contentType, out var frameSource))
            return new SmsOutboundEnqueueResult(false, null, false, "no_frame");

        var cap = string.IsNullOrWhiteSpace(caption)
            ? $"Victoria still ({frameSource})"
            : caption;

        return await EnqueueMmsAsync(
                kurt,
                bytes!,
                contentType!,
                cap,
                source ?? $"screenshot:{frameSource}",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public IReadOnlyList<SmsOutboundJob> ListPending(int limit = 10)
    {
        ExpireOld();
        lock (_gate)
        {
            return _jobs
                .Where(j => j.Status == SmsOutboundStatus.Pending)
                .OrderBy(j => j.CreatedUtc)
                .Take(Math.Clamp(limit, 1, 50))
                .Select(ClonePublic)
                .ToList();
        }
    }

    public bool TryAck(string jobId, bool success, string? error = null)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return false;

        lock (_gate)
        {
            var job = _jobs.FirstOrDefault(j =>
                string.Equals(j.Id, jobId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (job is null || job.Status != SmsOutboundStatus.Pending)
                return false;

            job.Status = success ? SmsOutboundStatus.Sent : SmsOutboundStatus.Failed;
            job.Error = success ? null : (error ?? "failed");
            job.ImageBytes = null;
            _logger.LogInformation(
                "SMS outbound ack id={Id} ok={Ok} err={Err}",
                job.Id,
                success,
                job.Error ?? "");
            return true;
        }
    }

    public IReadOnlyList<SmsOutboundJob> ListRecent(int limit = 50)
    {
        lock (_gate)
        {
            return _jobs
                .OrderByDescending(j => j.CreatedUtc)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(ClonePublic)
                .ToList();
        }
    }

    private string? FirstAllowlistedKurt()
    {
        var set = SmsE164.ParseAllowlist(_sms.KurtAllowlistE164);
        return set.Count == 0 ? null : set.OrderBy(x => x, StringComparer.Ordinal).First();
    }

    private bool TryCaptureStill(out byte[]? bytes, out string? contentType, out string source)
    {
        if (_browserView is not null && _browserView.TryGetImageBytes(out var b, out var ct) && b is { Length: > 0 })
        {
            bytes = b;
            contentType = ct;
            source = "browser";
            return true;
        }

        var desk = _desktopView?.TryGetImageBytes();
        if (desk is { Length: > 0 })
        {
            bytes = desk;
            var snap = _desktopView!.GetSnapshot();
            contentType = FormatToContentType(snap.Format);
            source = string.IsNullOrWhiteSpace(snap.Source) ? "presence" : snap.Source;
            return true;
        }

        bytes = null;
        contentType = null;
        source = "none";
        return false;
    }

    private static string FormatToContentType(string? format)
    {
        var f = (format ?? "jpeg").Trim().ToLowerInvariant();
        return f switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    private bool TryTakeSmsSlot(out string? error)
    {
        var now = DateTimeOffset.UtcNow;
        var minGap = Math.Max(0, _sms.MinSecondsBetweenSms);
        var maxHour = Math.Max(1, _sms.MaxSmsPerHour);
        lock (_gate)
        {
            PruneMarks(_smsMarks, now);
            if ((now - _lastSmsUtc).TotalSeconds < minGap)
            {
                error = "rate_min_gap_sms";
                return false;
            }

            if (_smsMarks.Count >= maxHour)
            {
                error = "rate_hourly_sms";
                return false;
            }

            _lastSmsUtc = now;
            _smsMarks.Add(now);
            error = null;
            return true;
        }
    }

    private bool TryTakeMmsSlot(out string? error)
    {
        var now = DateTimeOffset.UtcNow;
        var minGap = Math.Max(0, _sms.MinSecondsBetweenMms);
        var maxHour = Math.Max(1, _sms.MaxMmsPerHour);
        lock (_gate)
        {
            PruneMarks(_mmsMarks, now);
            if ((now - _lastMmsUtc).TotalSeconds < minGap)
            {
                error = "rate_min_gap_mms";
                return false;
            }

            if (_mmsMarks.Count >= maxHour)
            {
                error = "rate_hourly_mms";
                return false;
            }

            _lastMmsUtc = now;
            _mmsMarks.Add(now);
            error = null;
            return true;
        }
    }

    private static void PruneMarks(List<DateTimeOffset> marks, DateTimeOffset now)
    {
        marks.RemoveAll(t => (now - t) > TimeSpan.FromHours(1));
    }

    private SmsOutboundJob NewJob(
        SmsOutboundKind kind,
        string to,
        string? text,
        byte[]? image,
        string? contentType,
        string? source) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            ToE164 = to,
            Text = text,
            ImageBytes = image,
            ContentType = contentType,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = SmsOutboundStatus.Pending,
            Source = source
        };

    private void StorePending(SmsOutboundJob job)
    {
        lock (_gate)
        {
            _jobs.Add(job);
            TrimJobsUnlocked();
        }
    }

    private void RecordDropped(
        SmsOutboundKind kind,
        string to,
        string? text,
        byte[]? image,
        string? contentType,
        string? source,
        string? error)
    {
        var job = NewJob(kind, to, text, image, contentType, source);
        job.Status = SmsOutboundStatus.DroppedRateLimit;
        job.Error = error;
        lock (_gate)
        {
            _jobs.Add(job);
            TrimJobsUnlocked();
        }

        _logger.LogWarning(
            "SMS outbound rate-limited kind={Kind} to={To} err={Err}",
            kind,
            SmsE164.Redact(to),
            error);
    }

    private void TrimJobsUnlocked()
    {
        const int maxKeep = 200;
        if (_jobs.Count <= maxKeep)
            return;
        _jobs.RemoveRange(0, _jobs.Count - maxKeep);
    }

    private void ExpireOld()
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(5, _sms.PendingTtlMinutes));
        var cutoff = DateTimeOffset.UtcNow - ttl;
        lock (_gate)
        {
            foreach (var j in _jobs.Where(j =>
                         j.Status == SmsOutboundStatus.Pending && j.CreatedUtc < cutoff))
            {
                j.Status = SmsOutboundStatus.Failed;
                j.Error = "expired";
                j.ImageBytes = null;
            }
        }
    }

    private static SmsOutboundJob ClonePublic(SmsOutboundJob j) =>
        new()
        {
            Id = j.Id,
            Kind = j.Kind,
            ToE164 = j.ToE164,
            Text = j.Text,
            ContentType = j.ContentType,
            // Include image only while pending so the poller can send MMS.
            ImageBytes = j.Status == SmsOutboundStatus.Pending ? j.ImageBytes : null,
            CreatedUtc = j.CreatedUtc,
            Status = j.Status,
            Error = j.Error,
            Source = j.Source
        };

    private async Task TryWebhookAsync(SmsOutboundJob job, CancellationToken ct)
    {
        var url = (_sms.OutboundWebhookUrl ?? string.Empty).Trim();
        if (url.Length == 0 || _httpClientFactory is null)
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("sms-outbound-webhook");
            var payload = new Dictionary<string, object?>
            {
                ["id"] = job.Id,
                ["kind"] = job.Kind.ToString().ToLowerInvariant(),
                ["toE164"] = job.ToE164,
                ["text"] = job.Text,
                ["contentType"] = job.ContentType,
                ["source"] = job.Source
            };
            if (job.ImageBytes is { Length: > 0 })
                payload["imageBase64"] = Convert.ToBase64String(job.ImageBytes);

            using var resp = await client
                .PostAsJsonAsync(url, payload, ct)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SMS outbound webhook HTTP {Code} for id={Id}",
                    (int)resp.StatusCode,
                    job.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS outbound webhook failed id={Id}", job.Id);
        }
    }
}
