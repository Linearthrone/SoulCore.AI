using System.Text.Json;
using SoulCore.Adapters.Ws;

namespace SoulCore.Inference.Tools.Body;

/// <summary>
/// Model-callable move_to tool (BED-132 / BED-117). Absolute world path-follow via
/// <see cref="IUnrealVerbClient.MoveToAsync"/> → UE <c>move_to x y z</c> /
/// AIController <c>MoveToLocation</c>. Relative small steps remain on keyword loco /
/// <see cref="IUnrealVerbClient.LocoAsync"/>.
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
        Description: "Walk to an absolute world location (cm) via NavMesh path-following.",
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

        var payload = new { x, y, z, mode = "absolute_path_follow" };
        return await BodyToolBridge.InvokeAsync(
            ct2 => _unreal.MoveToAsync(payload, ct2),
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
              "description": "World X destination in cm (Unreal)."
            },
            "y": {
              "type": "number",
              "description": "World Y destination in cm (Unreal)."
            },
            "z": {
              "type": "number",
              "description": "World Z destination in cm (optional; projected to NavMesh when omitted/near floor)."
            }
          },
          "required": ["x", "y"]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
