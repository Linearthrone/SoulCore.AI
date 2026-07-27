using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Model-callable move_to tool (BED-132). Interim: wraps
/// <see cref="IUnrealVerbClient.LocoAsync"/> with relative offsets
/// (<c>forward=x</c>, <c>right=y</c>, <c>up=z</c> cm) — teleport-style via
/// UE <c>move_avatar_relative</c>. Absolute path-following requires BED-117
/// (<c>MoveToAsync</c> / AIController); not available yet.
/// </summary>
public sealed class MoveToTool : ITool
{
    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private readonly IUnrealVerbClient _unreal;

    public MoveToTool(IUnrealVerbClient unreal)
    {
        _unreal = unreal ?? throw new ArgumentNullException(nameof(unreal));
    }

    public ToolDefinition Definition { get; } = new(
        Name: "move_to",
        Description: "Walk to a location (interim: relative offset in cm via loco / move_avatar_relative; not absolute path-following yet).",
        Parameters: ParametersSchema);

    public async Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return new ToolResult(
                Success: false,
                Content: "error: move_to expects a JSON object with 'x' and 'y' numbers.",
                Data: null);
        }

        if (!TryReadNumber(args, "x", out var x))
        {
            return new ToolResult(
                Success: false,
                Content: "error: move_to requires 'x' (number).",
                Data: null);
        }

        if (!TryReadNumber(args, "y", out var y))
        {
            return new ToolResult(
                Success: false,
                Content: "error: move_to requires 'y' (number).",
                Data: null);
        }

        var z = 0.0;
        if (args.TryGetProperty("z", out _) && !TryReadNumber(args, "z", out z))
        {
            return new ToolResult(
                Success: false,
                Content: "error: move_to 'z' must be a number when provided.",
                Data: null);
        }

        // Interim (BED-117 not landed): treat x/y/z as relative local cm offsets.
        var payload = new { forward = x, right = y, up = z, mode = "relative_offset_interim" };
        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.LocoAsync(payload, ct2),
            data: payload,
            ct).ConfigureAwait(false);
    }

    private static bool TryReadNumber(JsonElement args, string name, out double value)
    {
        value = 0;
        if (!args.TryGetProperty(name, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out value))
            return true;

        if (prop.ValueKind == JsonValueKind.String
            && double.TryParse(
                prop.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
            return true;

        return false;
    }

    private static JsonElement BuildParametersSchema()
    {
        var json = """
        {
          "type": "object",
          "properties": {
            "x": {
              "type": "number",
              "description": "Forward offset in cm (interim relative loco; absolute world X when BED-117 path-following lands)."
            },
            "y": {
              "type": "number",
              "description": "Right offset in cm (interim relative loco)."
            },
            "z": {
              "type": "number",
              "description": "Up offset in cm (optional, default 0)."
            }
          },
          "required": ["x", "y"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
