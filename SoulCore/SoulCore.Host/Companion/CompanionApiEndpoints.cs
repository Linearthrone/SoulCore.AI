using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoulCore.Adapters.Ws;
using SoulCore.Config;
using SoulCore.Host.Ws;

namespace SoulCore.Host.Companion;

/// <summary>
/// Maps <c>/api/companion/v1/*</c> for Victoria Link (push + ComfyUI media).
/// Auth mirrors WS: Bearer / X-Api-Key when <c>SOULCORE_COMPANION_API_TOKEN</c> is set.
/// </summary>
public static class CompanionApiEndpoints
{
    public static IEndpointRouteBuilder MapCompanionApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companion/v1")
            .AddEndpointFilter(CompanionAuthFilter);

        group.MapGet("/contacts", (IConfiguration config) =>
        {
            var opts = config.GetSection(CompanionOptions.SectionName).Get<CompanionOptions>()
                ?? new CompanionOptions();
            return Results.Json(new
            {
                contacts = new[]
                {
                    new
                    {
                        id = opts.DefaultContactId,
                        name = opts.DefaultContactName,
                        isPrimary = true,
                        description = "Victoria (SoulCore). Extra personas reserved for a future external service."
                    }
                }
            });
        });

        group.MapPost("/messages/push", async (
            HttpRequest request,
            ICompanionOutboundMessenger outbound,
            CancellationToken ct) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                .ConfigureAwait(false);
            var root = doc.RootElement;
            var text = root.TryGetProperty("text", out var t) ? t.GetString() : null;
            var contactId = root.TryGetProperty("contactId", out var c) ? c.GetString() : null;
            var mediaId = root.TryGetProperty("mediaId", out var m) ? m.GetString() : null;
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(mediaId))
                return Results.BadRequest(new { error = "text or mediaId required" });

            var body = string.IsNullOrWhiteSpace(text)
                ? "I made something for you."
                : text!;
            var result = await outbound
                .PushAsync(body, contactId, mediaId, streamDelta: false, ct)
                .ConfigureAwait(false);
            return result.Ok
                ? Results.Json(new
                {
                    ok = true,
                    frameId = result.FrameId,
                    contactId = result.ContactId,
                    mediaId = result.MediaId
                })
                : Results.BadRequest(new { error = result.Error });
        });

        // PROP-1.2: tablet SMS/MMS gateway → One Thread (presence-local), no tools.
        // Never return empty 500: JsonException → 400 JSON; unexpected → 500 JSON; model_down → 503.
        group.MapPost("/messages/inbound", async (
            HttpRequest request,
            ISmsInboundService inbound,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("SoulCore.Host.Companion.Inbound");

            try
            {
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                    .ConfigureAwait(false);
                var root = doc.RootElement;
                var from = root.TryGetProperty("fromE164", out var f) ? f.GetString()
                    : root.TryGetProperty("from", out var f2) ? f2.GetString() : null;
                var text = root.TryGetProperty("text", out var t) ? t.GetString() : null;
                var contentType = root.TryGetProperty("contentType", out var ctEl)
                    ? ctEl.GetString()
                    : root.TryGetProperty("imageContentType", out var ict) ? ict.GetString() : null;

                byte[]? imageBytes = null;
                if (root.TryGetProperty("imageBase64", out var b64) && b64.ValueKind == JsonValueKind.String)
                {
                    var s = b64.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        try
                        {
                            imageBytes = Convert.FromBase64String(s.Trim());
                        }
                        catch (FormatException)
                        {
                            return Results.BadRequest(new { ok = false, error = "imageBase64 invalid" });
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(from))
                    return Results.BadRequest(new { ok = false, error = "fromE164 required" });

                var result = await inbound
                    .HandleAsync(new SmsInboundRequest(from!, text, imageBytes, contentType), ct)
                    .ConfigureAwait(false);

                if (!result.Ok)
                {
                    if (string.Equals(result.Error, "chat.model_down", StringComparison.Ordinal))
                    {
                        return Results.Json(
                            new { ok = false, error = result.Error, mediaId = result.MediaId },
                            statusCode: StatusCodes.Status503ServiceUnavailable);
                    }

                    return Results.BadRequest(new { ok = false, error = result.Error });
                }

                return Results.Json(new
                {
                    ok = true,
                    dropped = result.Dropped,
                    replyText = result.ReplyText,
                    mediaId = result.MediaId,
                    frameId = result.FrameId,
                    stub = result.UsedStub,
                    provider = result.Provider,
                    sessionId = "presence-local",
                    outboundSmsJobId = result.OutboundSmsJobId,
                    outboundMmsJobId = result.OutboundMmsJobId
                });
            }
            catch (JsonException jex)
            {
                log.LogWarning(jex, "Companion inbound rejected: invalid JSON body");
                return Results.BadRequest(new { ok = false, error = "invalid JSON body" });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Companion inbound unexpected failure");
                return Results.Json(
                    new { ok = false, error = "inbound_failed" },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        // PROP-1.3: tablet gateway polls outbound SMS/MMS jobs (mockable queue).
        group.MapGet("/sms/outbound/pending", (
            ISmsOutboundService outbound,
            int? limit) =>
        {
            var jobs = outbound.ListPending(limit ?? 10);
            return Results.Json(new
            {
                ok = true,
                jobs = jobs.Select(j => new
                {
                    id = j.Id,
                    kind = j.Kind.ToString().ToLowerInvariant(),
                    toE164 = j.ToE164,
                    text = j.Text,
                    contentType = j.ContentType,
                    imageBase64 = j.ImageBytes is { Length: > 0 }
                        ? Convert.ToBase64String(j.ImageBytes)
                        : null,
                    createdUtc = j.CreatedUtc,
                    source = j.Source
                })
            });
        });

        group.MapPost("/sms/outbound/{jobId}/ack", async (
            string jobId,
            HttpRequest request,
            ISmsOutboundService outbound,
            CancellationToken ct) =>
        {
            var success = true;
            string? error = null;
            try
            {
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                    .ConfigureAwait(false);
                var root = doc.RootElement;
                if (root.TryGetProperty("ok", out var okEl))
                {
                    success = okEl.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => !string.Equals(
                            okEl.GetString(), "false", StringComparison.OrdinalIgnoreCase),
                        _ => true
                    };
                }

                if (root.TryGetProperty("error", out var errEl) && errEl.ValueKind == JsonValueKind.String)
                    error = errEl.GetString();
            }
            catch (JsonException)
            {
                // Empty body = success ack.
            }

            var acked = outbound.TryAck(jobId, success, error);
            return acked
                ? Results.Json(new { ok = true, jobId })
                : Results.NotFound(new { ok = false, error = "job_not_found" });
        });

        group.MapGet("/media/models", async (ICompanionMediaService media, CancellationToken ct) =>
        {
            var models = await media.ListModelsAsync(ct).ConfigureAwait(false);
            return Results.Json(new { models });
        });

        group.MapPost("/media/generate", async (
            HttpRequest request,
            ICompanionMediaService media,
            CancellationToken ct) =>
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                .ConfigureAwait(false);
            var root = doc.RootElement;
            var positive = root.TryGetProperty("positivePrompt", out var p) ? p.GetString()
                : root.TryGetProperty("prompt", out var p2) ? p2.GetString() : null;
            var negative = root.TryGetProperty("negativePrompt", out var n) ? n.GetString() : null;
            var model = root.TryGetProperty("model", out var mo) ? mo.GetString() : null;
            var contactId = root.TryGetProperty("contactId", out var c) ? c.GetString() : null;
            var pushToChat = root.TryGetProperty("pushToChat", out var push) && push.ValueKind == JsonValueKind.True;
            var caption = root.TryGetProperty("caption", out var cap) ? cap.GetString() : null;

            if (string.IsNullOrWhiteSpace(positive))
                return Results.BadRequest(new { error = "positivePrompt required" });

            try
            {
                var asset = await media
                    .GenerateAsync(positive!, negative, model, contactId, ct)
                    .ConfigureAwait(false);
                if (pushToChat)
                {
                    await media
                        .PushGeneratedToChatAsync(asset.MediaId, caption, ct)
                        .ConfigureAwait(false);
                }

                return Results.Json(new
                {
                    ok = true,
                    mediaId = asset.MediaId,
                    contactId = asset.ContactId,
                    contentType = asset.ContentType,
                    sizeBytes = asset.SizeBytes,
                    fileUrl = $"/api/companion/v1/media/{asset.MediaId}/file"
                });
            }
            catch (Exception ex)
            {
                return Results.Json(
                    new { ok = false, error = ex.Message },
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapGet("/media/{mediaId}/file", (
            string mediaId,
            ICompanionMediaService media) =>
        {
            if (!media.TryGetFile(mediaId, out var path, out var meta) || meta is null)
                return Results.NotFound(new { error = "media not found" });
            return Results.File(path, meta.ContentType, meta.FileName);
        });

        // --- Video call (FED-192): waist-up Victoria frames; WebRTC later ---
        group.MapPost("/call/session", async (
            HttpRequest request,
            CompanionCallSessionStore sessions,
            CancellationToken ct) =>
        {
            string? contactId = null;
            if (request.ContentLength is > 0)
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                        .ConfigureAwait(false);
                    contactId = doc.RootElement.TryGetProperty("contactId", out var c)
                        ? c.GetString()
                        : null;
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "invalid JSON body" });
                }
            }

            var session = sessions.Start(contactId);
            return Results.Json(new
            {
                ok = true,
                sessionId = session.SessionId,
                contactId = session.ContactId,
                mode = session.Mode,
                pollUrl = $"/api/companion/v1/call/frame?sessionId={session.SessionId}",
                webrtc = new
                {
                    available = session.WebrtcAvailable,
                    note = "WebRTC signaling deferred — MVP polls call_capture waist-up frames from Unreal (REX-01 TASK-192)."
                }
            });
        });

        group.MapGet("/call/frame", async (
            string? sessionId,
            bool? fallbackEyes,
            CompanionCallSessionStore sessions,
            IUnrealCallCameraClient callCam,
            IUnrealEyeCaptureClient eyes,
            ILoggerFactory logFactory,
            CancellationToken ct) =>
        {
            var log = logFactory.CreateLogger("SoulCore.Host.Companion.Call");
            if (!string.IsNullOrWhiteSpace(sessionId) && !sessions.TryGet(sessionId, out _))
                return Results.NotFound(new { error = "unknown sessionId" });

            var frame = await callCam.CaptureCallFrameAsync(ct).ConfigureAwait(false);
            var source = "call_capture";
            var safeSessionIdForLog = string.IsNullOrEmpty(sessionId)
                ? "(none)"
                : sessionId.Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (frame is null && fallbackEyes == true)
            {
                frame = await eyes.CaptureEyeAsync(ct).ConfigureAwait(false);
                source = "eyes_fallback";
            }

            if (frame is null || frame.Bytes.Length == 0)
            {
                log.LogDebug(
                    "call/frame empty (session={Session}) — UE call_capture not ready?",
                    safeSessionIdForLog);
                return Results.Json(
                    new
                    {
                        ok = false,
                        error = "no_frame",
                        hint = "Start PIE with Victoria + REX call camera (call_capture). Optional: ?fallbackEyes=true"
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var contentType = string.Equals(frame.Format, "jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(frame.Format, "jpeg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : "image/png";
            log.LogDebug("call/frame ok source={Source} {W}x{H}", source, frame.Width, frame.Height);
            return Results.Bytes(
                frame.Bytes,
                contentType,
                fileDownloadName: $"victoria-call.{(contentType.Contains("jpeg") ? "jpg" : "png")}");
        });

        group.MapDelete("/call/session/{sessionId}", (
            string sessionId,
            CompanionCallSessionStore sessions) =>
        {
            var removed = sessions.End(sessionId);
            return removed
                ? Results.Json(new { ok = true, sessionId })
                : Results.NotFound(new { error = "unknown sessionId" });
        });

        return app;
    }

    private static async ValueTask<object?> CompanionAuthFilter(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var config = http.RequestServices.GetService<IConfiguration>();
        var token = CompanionWsAuth.ResolveConfiguredToken(config);
        var outcome = CompanionWsAuth.Evaluate(http.Request, token);
        if (outcome is CompanionWsAuth.AuthOutcome.Missing or CompanionWsAuth.AuthOutcome.Invalid)
        {
            var logger = http.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SoulCore.Host.Companion.Auth");
            logger.LogWarning(
                "Companion API rejected ({Safe})",
                CompanionWsAuth.FormatLogSafe(outcome, CompanionWsAuth.DescribeHeaderSource(http.Request)));
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }
}
