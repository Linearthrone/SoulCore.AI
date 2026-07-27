using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Model-callable set_emotion tool (BED-132). Wraps
/// <see cref="IUnrealVerbClient.SetEmotionAsync"/> with named emotion presets
/// mapped to valence/arousal/dominance + label.
/// </summary>
public sealed class SetEmotionTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private static readonly Dictionary<string, EmotionPreset> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["neutral"] = new(0.0, 0.25, 0.0, "neutral"),
            ["happy"] = new(0.7, 0.55, 0.3, "happy"),
            ["sad"] = new(-0.6, 0.25, -0.2, "sad"),
            ["angry"] = new(-0.55, 0.8, 0.55, "angry"),
            ["curious"] = new(0.25, 0.6, 0.2, "curious"),
        };

    private readonly IUnrealVerbClient _unreal;

    public SetEmotionTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "set_emotion",
        Description: "Set current emotion.",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: set_emotion expects a JSON object with an 'emotion' string.",
                Data: null);
        }

        if (!args.TryGetProperty("emotion", out var emotionProp) || emotionProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: set_emotion requires 'emotion' (string).",
                Data: null);
        }

        var emotion = emotionProp.GetString();
        if (string.IsNullOrWhiteSpace(emotion))
        {
            return new ToolResult(
                Success: false,
                Content: "error: set_emotion 'emotion' must be non-empty.",
                Data: null);
        }

        if (!Presets.TryGetValue(emotion.Trim(), out var preset))
        {
            return new ToolResult(
                Success: false,
                Content: "error: set_emotion 'emotion' must be one of: neutral, happy, sad, angry, curious.",
                Data: null);
        }

        var payload = new
        {
            valence = preset.Valence,
            arousal = preset.Arousal,
            dominance = preset.Dominance,
            label = preset.Label
        };

        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.SetEmotionAsync(payload, ct2),
            data: payload,
            ct).ConfigureAwait(false);
    }

    private readonly record struct EmotionPreset(
        double Valence,
        double Arousal,
        double Dominance,
        string Label);

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "emotion": {
              "type": "string",
              "description": "neutral | happy | sad | angry | curious"
            }
          },
          "required": ["emotion"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
