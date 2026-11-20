using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SoulCore.Adapters.Ws;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

public static class VoiceApiEndpoints
{
    public static IEndpointRouteBuilder MapVoiceApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/stt", async (HttpRequest request, ISttClient stt, IOptions<VoiceOptions> options) =>
        {
            if (!options.Value.Enabled)
                return Results.Json(new { ok = false, error = "voice disabled" }, statusCode: 503);
            if (!options.Value.HasApiKey)
                return Results.Json(new { ok = false, error = "SONIOX_API_KEY not configured" }, statusCode: 503);

            if (!request.HasFormContentType)
                return Results.BadRequest(new { ok = false, error = "expected multipart form with 'audio' or 'file'" });

            var form = await request.ReadFormAsync().ConfigureAwait(false);
            var file = form.Files.GetFile("audio") ?? form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { ok = false, error = "missing audio file" });

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms).ConfigureAwait(false);
            var bytes = ms.ToArray();

            try
            {
                var text = await stt.TranscribeAsync(bytes, file.FileName, request.HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                return Results.Json(new { ok = true, text, provider = "soniox" });
            }
            catch (Exception ex)
            {
                return Results.Json(new { ok = false, error = ex.Message }, statusCode: 502);
            }
        });

        app.MapGet("/api/voice/last.wav", (IVoiceSpeakService voice) =>
        {
            var wav = voice.LastWav;
            if (wav is null || wav.Length == 0)
                return Results.NotFound();
            return Results.File(wav, "audio/wav", "last.wav");
        });

        app.MapGet("/api/voice/health", (IOptions<VoiceOptions> options) =>
        {
            var opts = options.Value;
            var keyOk = opts.HasApiKey;
            return Results.Json(new
            {
                enabled = opts.Enabled,
                provider = "soniox",
                apiKeyConfigured = keyOk,
                stt = new
                {
                    url = opts.SttWsUrl,
                    asyncModel = opts.SttAsyncModel,
                    realtimeModel = opts.SttRealtimeModel,
                    ok = opts.Enabled && keyOk
                },
                tts = new
                {
                    url = opts.TtsWsUrl,
                    model = opts.TtsModel,
                    voice = opts.DefaultVoice,
                    ok = opts.Enabled && keyOk
                },
                playOnHostSpeakers = opts.PlayOnHostSpeakers,
                playInUnreal = opts.PlayInUnreal
            });
        });

        return app;
    }
}
