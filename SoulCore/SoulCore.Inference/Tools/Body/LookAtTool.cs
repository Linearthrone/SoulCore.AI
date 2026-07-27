using System.Globalization;
using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Model-callable look_at tool (BED-132). Wraps <see cref="IUnrealVerbClient.LookAsync"/>.
/// The UE mapper currently ignores payload and always sends <c>look_at_player</c>;
/// world-point targets are accepted for schema stability and forwarded as payload.
/// </summary>
public sealed class LookAtTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IUnrealVerbClient _unreal;

    public LookAtTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "look_at",
        Description: "Look at a target (player or world point).",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: look_at expects a JSON object with a 'target' string.",
                Data: null);
        }

        if (!args.TryGetProperty("target", out var targetProp) || targetProp.ValueKind != JsonValueKind.String)
        {
            return new ToolResult(
                Success: false,
                Content: "error: look_at requires 'target' (string: 'player' or 'x,y,z').",
                Data: null);
        }

        var target = targetProp.GetString();
        if (string.IsNullOrWhiteSpace(target))
        {
            return new ToolResult(
                Success: false,
                Content: "error: look_at 'target' must be non-empty.",
                Data: null);
        }

        var trimmed = target.Trim();
        object payload;
        if (trimmed.Equals("player", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("look_at_player", StringComparison.OrdinalIgnoreCase))
        {
            payload = new { target = "player" };
        }
        else if (TryParsePoint(trimmed, out var x, out var y, out var z))
        {
            payload = new { target = "point", x, y, z };
        }
        else
        {
            return new ToolResult(
                Success: false,
                Content: "error: look_at 'target' must be 'player' or 'x,y,z' (three numbers).",
                Data: null);
        }

        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.LookAsync(payload, ct2),
            data: payload,
            ct).ConfigureAwait(false);
    }

    private static bool TryParsePoint(string target, out double x, out double y, out double z)
    {
        x = y = z = 0;
        var parts = target.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;

        return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z);
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "target": {
              "type": "string",
              "description": "player | x,y,z — look at the player or a world point (UE currently maps all looks to look_at_player)."
            }
          },
          "required": ["target"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
