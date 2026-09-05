using System.Text.Json;
using SoulCore.Inference.Clients;
using SoulCore.Inference.Tooling;

namespace SoulCore.Host.Companion;

/// <summary>
/// PROP-1.3: model-callable MMS still to Kurt (opt-in / on ask — not every screenshot).
/// Prefer Victoria browser frame, else Presence desktop hub.
/// </summary>
public sealed class SendScreenshotMmsTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly ISmsOutboundService _outbound;

    public SendScreenshotMmsTool(ISmsOutboundService outbound)
    {
        _outbound = outbound ?? throw new ArgumentNullException(nameof(outbound));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "send_screenshot_mms",
        Description:
            "Send Kurt one MMS still of Victoria's current browser or Presence frame. " +
            "Use only when Kurt explicitly asks for a screenshot / still / pic of what she sees. " +
            "Do not call after every desktop_screenshot or tool click.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        string? caption = null;
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("caption", out var c)
            && c.ValueKind == JsonValueKind.String)
        {
            caption = c.GetString();
        }

        var result = await _outbound
            .EnqueueScreenshotMmsToKurtAsync(caption, source: "tool:send_screenshot_mms", ct)
            .ConfigureAwait(false);

        if (!result.Ok)
        {
            return new ToolResult(
                Success: false,
                Content: result.RateLimited
                    ? $"rate limited: {result.Error}"
                    : $"send_screenshot_mms failed: {result.Error ?? "unknown"}",
                Data: new { result.Error, result.RateLimited });
        }

        return new ToolResult(
            Success: true,
            Content: $"queued MMS still jobId={result.JobId} for Kurt (tablet gateway will send)",
            Data: new { jobId = result.JobId });
    }

    private static JsonElement BuildParametersSchema()
    {
        const string json = """
            {
              "type": "object",
              "properties": {
                "caption": {
                  "type": "string",
                  "description": "Optional short MMS caption"
                }
              },
              "additionalProperties": false
            }
            """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}

/// <summary>Keyword helper for SMS inbound screenshot asks (no tools on that path).</summary>
public static class SmsScreenshotAsk
{
    public static bool LooksLikeScreenshotAsk(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim().ToLowerInvariant();
        ReadOnlySpan<string> needles =
        [
            "screenshot",
            "screen shot",
            "send me a pic",
            "send me a still",
            "send a still",
            "send me a photo",
            "what do you see",
            "show me what you see",
            "mms still",
            "send still"
        ];
        foreach (var n in needles)
        {
            if (t.Contains(n, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
