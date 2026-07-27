using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Model-callable play_animation tool (BED-132). Wraps
/// <see cref="IUnrealVerbClient.PlayAnimationAsync"/>. Name aliases match
/// <c>DetectAnimationIntent</c> (see <see cref="AnimationNameMap"/>).
/// </summary>
public sealed class PlayAnimationTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IUnrealVerbClient _unreal;

    public PlayAnimationTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "play_animation",
        Description: "Play a body animation (wave, nod, etc.).",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: play_animation expects a JSON object with a 'name' string.",
                Data: null);
        }

        if (!args.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: play_animation requires 'name' (string).",
                Data: null);
        }

        var raw = nameProp.GetString();
        var resolved = AnimationNameMap.Resolve(raw);
        if (resolved is null)
        {
            return new ToolResult(
                Success: false,
                Content: "error: play_animation 'name' must be non-empty.",
                Data: null);
        }

        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.PlayAnimationAsync(resolved, ct2),
            data: new { name = resolved, requested = raw },
            ct).ConfigureAwait(false);
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "name": {
              "type": "string",
              "description": "Animation name, e.g. wave, nod, wave_goodbye, shake_head, thumbs_up, bow, clap, dance, laugh, point, jump, sit, stand."
            }
          },
          "required": ["name"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
