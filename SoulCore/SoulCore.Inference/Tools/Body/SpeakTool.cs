using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Model-callable speak tool (BED-132). Wraps <see cref="IUnrealVerbClient.SpeakAsync"/>.
/// </summary>
/// <remarks>
/// The chat handler already auto-speaks the final reply via TTS. This tool is for
/// <b>additional mid-loop utterances</b> (interjections) the model wants spoken
/// before the final reply — e.g. "one moment" while walking. Prefer the final
/// reply text for the main spoken answer; use <c>speak</c> only for extras.
/// </remarks>
public sealed class SpeakTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IUnrealVerbClient _unreal;

    public SpeakTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "speak",
        Description: "Speak text aloud with TTS. Use for additional mid-loop utterances; the final chat reply is already auto-spoken.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: speak expects a JSON object with a 'text' string.",
                Data: null);
        }

        if (!args.TryGetProperty("text", out var textProp) || textProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: speak requires 'text' (string).",
                Data: null);
        }

        var text = textProp.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ToolResult(
                Success: false,
                Content: "error: speak 'text' must be non-empty.",
                Data: null);
        }

        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.SpeakAsync(text, ct2),
            data: new { text },
            ct).ConfigureAwait(false);
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "text": {
              "type": "string",
              "description": "Text to speak aloud (TTS). Prefer for mid-loop interjections; final reply is auto-spoken."
            }
          },
          "required": ["text"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
