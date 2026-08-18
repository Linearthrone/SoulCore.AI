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
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct)
                    .ConfigureAwait(false);
                contactId = doc.RootElement.TryGetProperty("contactId", out var c)
                    ? c.GetString()
                    : null;
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
            if (frame is null && fallbackEyes == true)
            {
                frame = await eyes.CaptureEyeAsync(ct).ConfigureAwait(false);
                source = "eye_capture_fallback";
            }

            if (frame is null || frame.Bytes.Length == 0)
            {
                log.LogDebug(
                    "call/frame empty (session={Session}) — UE call_capture not ready?",
                    sessionId ?? "(none)");
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
