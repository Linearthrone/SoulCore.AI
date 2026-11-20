using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;

namespace SoulCore.Host.Voice;

/// <summary>PROP-21: Soniox realtime TTS WebSocket (streaming + collect-to-bytes).</summary>
public sealed class SonioxTtsClient : ITtsClient
{
    private readonly VoiceOptions _options;
    private readonly ILogger<SonioxTtsClient> _logger;

    public SonioxTtsClient(IOptions<VoiceOptions> options, ILogger<SonioxTtsClient> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]?> SynthesizeAsync(
        string text,
        string? voice = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var chunks = new List<byte[]>();
        await SynthesizeStreamingAsync(
            text,
            (chunk, end, ct) =>
            {
                if (chunk.Length > 0)
                    chunks.Add(chunk.ToArray());
                return Task.CompletedTask;
            },
            voice,
            cancellationToken).ConfigureAwait(false);

        if (chunks.Count == 0)
            return null;

        var pcm = Concat(chunks);
        return WrapPcmAsWav(pcm, _options.TtsSampleRate);
    }

    public async Task SynthesizeStreamingAsync(
        string text,
        Func<ReadOnlyMemory<byte>, bool, CancellationToken, Task> onAudio,
        string? voice = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onAudio);
        var speak = SonioxSpeechText.NormalizeForSpeak(text);
        if (string.IsNullOrWhiteSpace(speak))
            return;
        if (!_options.HasApiKey)
            throw new InvalidOperationException("Voice:ApiKey / SONIOX_API_KEY is not configured");

        var voiceId = string.IsNullOrWhiteSpace(voice) ? _options.DefaultVoice : voice.Trim();
        var streamId = "s-" + Guid.NewGuid().ToString("N")[..12];

        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", "Bearer " + _options.ApiKey.Trim());
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, _options.TimeoutSeconds)));

        await ws.ConnectAsync(new Uri(_options.TtsWsUrl), linked.Token).ConfigureAwait(false);

        var config = JsonSerializer.Serialize(new
        {
            model = _options.TtsModel,
            language = _options.TtsLanguage,
            voice = voiceId,
            audio_format = _options.TtsAudioFormat,
            sample_rate = _options.TtsSampleRate,
            stream_id = streamId
        });
        await SendTextAsync(ws, config, linked.Token).ConfigureAwait(false);

        // Chunk text for lower first-byte latency.
        foreach (var piece in ChunkText(speak, 80))
        {
            var msg = JsonSerializer.Serialize(new
            {
                text = piece,
                text_end = false,
                stream_id = streamId
            });
            await SendTextAsync(ws, msg, linked.Token).ConfigureAwait(false);
        }

        var endMsg = JsonSerializer.Serialize(new
        {
            text = "",
            text_end = true,
            stream_id = streamId
        });
        await SendTextAsync(ws, endMsg, linked.Token).ConfigureAwait(false);

        var buffer = new byte[64 * 1024];
        while (ws.State == WebSocketState.Open && !linked.Token.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, linked.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
                continue;

            var json = Encoding.UTF8.GetString(ms.ToArray());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_type", out _) || root.TryGetProperty("error_code", out _))
            {
                _logger.LogWarning("Soniox TTS error: {Body}", Truncate(json));
                throw new InvalidOperationException("Soniox TTS error: " + Truncate(json));
            }

            var audioEnd = root.TryGetProperty("audio_end", out var ae) && ae.ValueKind == JsonValueKind.True;
            var terminated = root.TryGetProperty("terminated", out var term) && term.ValueKind == JsonValueKind.True;

            if (root.TryGetProperty("audio", out var audioProp)
                && audioProp.ValueKind == JsonValueKind.String)
            {
                var b64 = audioProp.GetString();
                if (!string.IsNullOrEmpty(b64))
                {
                    var bytes = Convert.FromBase64String(b64);
                    await onAudio(bytes, audioEnd || terminated, linked.Token).ConfigureAwait(false);
                }
            }
            else if (audioEnd || terminated)
            {
                await onAudio(ReadOnlyMemory<byte>.Empty, true, linked.Token).ConfigureAwait(false);
            }

            if (terminated || audioEnd)
                return;
        }
    }

    private static async Task SendTextAsync(ClientWebSocket ws, string text, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private static IEnumerable<string> ChunkText(string text, int size)
    {
        if (text.Length <= size)
        {
            yield return text;
            yield break;
        }

        for (var i = 0; i < text.Length; i += size)
            yield return text[i..Math.Min(i + size, text.Length)];
    }

    private static byte[] Concat(List<byte[]> parts)
    {
        var len = 0;
        foreach (var p in parts)
            len += p.Length;
        var all = new byte[len];
        var o = 0;
        foreach (var p in parts)
        {
            Buffer.BlockCopy(p, 0, all, o, p.Length);
            o += p.Length;
        }

        return all;
    }

    /// <summary>Wrap raw PCM s16le mono as a WAV for Host SoundPlayer / last.wav.</summary>
    internal static byte[] WrapPcmAsWav(byte[] pcm, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = pcm.Length;
        var fileSize = 36 + dataSize;

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(fileSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        bw.Write(pcm);
        return ms.ToArray();
    }

    private static string Truncate(string s) =>
        s.Length <= 200 ? s : s[..197] + "...";
}
